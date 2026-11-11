using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    public enum TipoSonido
    {
        Disparo, Canon, Electrico, Explosion, Baja, Construir, Mejora, Oleada, Victoria, Derrota, Error, Habilidad, Click
    }

    /// <summary>
    /// "Game feel": números de daño flotantes, partículas, ondas expansivas, relámpagos,
    /// destellos, sacudida de cámara y sonidos sintetizados. Todo se genera por código.
    /// </summary>
    public class EfectosVisualesManager : MonoBehaviour
    {
        public static EfectosVisualesManager Instance { get; private set; }

        public bool sonidoActivo = true;
        public bool sacudidaActiva = true;

        private class TextoFx { public TextMesh tm; public float t, dur, escala; public Vector3 vel; public Color color; }
        private class OndaFx { public LineRenderer lr; public Vector3 centro; public float radio, t, dur; public Color color; }
        private class RayoFx { public LineRenderer lr; public float t, dur; public Color color; }
        private class DestelloFx { public SpriteRenderer sr; public float t, dur, tam; public Color color; }

        private readonly List<TextoFx> textos = new List<TextoFx>();
        private readonly Stack<TextMesh> poolTextos = new Stack<TextMesh>();
        private readonly List<OndaFx> ondas = new List<OndaFx>();
        private readonly Stack<LineRenderer> poolLineas = new Stack<LineRenderer>();
        private readonly List<RayoFx> rayos = new List<RayoFx>();
        private readonly List<DestelloFx> destellos = new List<DestelloFx>();
        private readonly Stack<SpriteRenderer> poolDestellos = new Stack<SpriteRenderer>();

        private ParticleSystem particulas;
        private Camera cam;
        private Vector3 camBase;
        private bool sacudiendo = false;
        private float shakeT, shakeDur, shakeInt;

        private AudioSource fuenteAudio;
        private readonly Dictionary<TipoSonido, AudioClip> clips = new Dictionary<TipoSonido, AudioClip>();
        private readonly Dictionary<TipoSonido, float> ultimoSonido = new Dictionary<TipoSonido, float>();

        public static EfectosVisualesManager Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("EfectosVisualesManager");
            return go.AddComponent<EfectosVisualesManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CrearSistemaParticulas();
            CrearAudio();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region Texto Flotante

        public void TextoFlotante(Vector3 pos, string texto, Color color, float escala = 1f)
        {
            if (textos.Count > 70) return; // límite para no saturar la pantalla

            TextMesh tm;
            if (poolTextos.Count > 0)
            {
                tm = poolTextos.Pop();
                tm.gameObject.SetActive(true);
            }
            else
            {
                tm = GraficosProc.CrearTextoMundo(transform, "", Color.white, 0.32f, 30);
            }

            tm.text = texto;
            tm.color = color;
            tm.transform.position = new Vector3(pos.x, pos.y, 0f);
            tm.transform.localScale = Vector3.one * escala;
            textos.Add(new TextoFx
            {
                tm = tm, t = 0f, dur = 0.85f, escala = escala, color = color,
                vel = new Vector3(Random.Range(-0.25f, 0.25f), 1.5f, 0f)
            });
        }

        #endregion

        #region Partículas, Ondas, Rayos y Destellos

        private void CrearSistemaParticulas()
        {
            GameObject go = new GameObject("Particulas");
            go.transform.SetParent(transform, false);
            particulas = go.AddComponent<ParticleSystem>();
            particulas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particulas.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.14f;
            main.gravityModifier = 0f;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule em = particulas.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule sh = particulas.shape;
            sh.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = particulas.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystem.SizeOverLifetimeModule sz = particulas.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            Material mat = new Material(GraficosProc.MaterialLinea);
            mat.mainTexture = GraficosProc.Brillo.texture;
            rend.sharedMaterial = mat;
            rend.sortingOrder = 16;
            particulas.Play();
        }

        public void Explosion(Vector3 pos, Color color, int cantidad = 12, float fuerza = 3f)
        {
            if (particulas == null) return;
            for (int i = 0; i < cantidad; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(fuerza * 0.35f, fuerza);
                ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = new Vector3(dir.x, dir.y, 0f),
                    startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f)),
                    startSize = Random.Range(0.1f, 0.24f),
                    startLifetime = Random.Range(0.25f, 0.6f)
                };
                particulas.Emit(ep, 1);
            }
        }

        private LineRenderer TomarLinea(int orden)
        {
            LineRenderer lr;
            if (poolLineas.Count > 0)
            {
                lr = poolLineas.Pop();
                lr.gameObject.SetActive(true);
            }
            else
            {
                lr = GraficosProc.CrearLinea(transform, "LineaFx", Color.white, Color.white, 0.06f, orden);
            }
            lr.sortingOrder = orden;
            lr.loop = false;
            return lr;
        }

        private void DevolverLinea(LineRenderer lr)
        {
            lr.gameObject.SetActive(false);
            poolLineas.Push(lr);
        }

        /// <summary>Anillo que se expande y se desvanece (impactos de cañón, pulsos, mejoras).</summary>
        public void OndaExpansiva(Vector3 pos, float radio, Color color, float duracion = 0.35f)
        {
            if (ondas.Count > 40) return;
            LineRenderer lr = TomarLinea(13);
            ondas.Add(new OndaFx { lr = lr, centro = pos, radio = radio, t = 0f, dur = duracion, color = color });
        }

        /// <summary>Relámpago zigzagueante entre dos puntos (Bobina Tesla).</summary>
        public void Rayo(Vector3 a, Vector3 b, Color color, float duracion = 0.15f, float ancho = 0.08f)
        {
            if (rayos.Count > 60) return;
            LineRenderer lr = TomarLinea(15);
            int segmentos = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / 0.35f), 3, 14);
            lr.positionCount = segmentos + 1;
            Vector3 dir = (b - a).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
            for (int i = 0; i <= segmentos; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)segmentos);
                if (i > 0 && i < segmentos) p += perp * Random.Range(-0.2f, 0.2f);
                lr.SetPosition(i, p);
            }
            lr.startWidth = ancho;
            lr.endWidth = ancho * 0.6f;
            rayos.Add(new RayoFx { lr = lr, t = 0f, dur = duracion, color = color });
        }

        /// <summary>Destello breve (boca del cañón, impactos).</summary>
        public void Destello(Vector3 pos, Color color, float tam = 0.4f, float duracion = 0.09f)
        {
            if (destellos.Count > 60) return;
            SpriteRenderer sr;
            if (poolDestellos.Count > 0)
            {
                sr = poolDestellos.Pop();
                sr.gameObject.SetActive(true);
            }
            else
            {
                sr = GraficosProc.CrearSprite(transform, "Destello", GraficosProc.Brillo, Color.white, 17);
            }
            sr.transform.position = pos;
            destellos.Add(new DestelloFx { sr = sr, t = 0f, dur = duracion, tam = tam, color = color });
        }

        #endregion

        #region Sacudida de Cámara

        public void SacudirCamara(float intensidad, float duracion)
        {
            if (!sacudidaActiva) return;
            if (cam == null) cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null) return;

            if (!sacudiendo)
            {
                camBase = cam.transform.position;
                sacudiendo = true;
                shakeT = 0f;
                shakeDur = duracion;
                shakeInt = intensidad;
            }
            else
            {
                shakeInt = Mathf.Max(shakeInt, intensidad);
                shakeDur = Mathf.Max(shakeDur - shakeT, duracion);
                shakeT = 0f;
            }
        }

        #endregion

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = textos.Count - 1; i >= 0; i--)
            {
                TextoFx f = textos[i];
                f.t += dt;
                float k = f.t / f.dur;
                f.tm.transform.position += f.vel * dt;
                f.vel *= 1f - Mathf.Clamp01(dt * 2.2f);
                float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
                f.tm.transform.localScale = Vector3.one * f.escala * pop;
                Color c = f.color;
                c.a = k < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.6f) / 0.4f);
                f.tm.color = c;
                if (f.t >= f.dur)
                {
                    f.tm.gameObject.SetActive(false);
                    poolTextos.Push(f.tm);
                    textos.RemoveAt(i);
                }
            }

            for (int i = ondas.Count - 1; i >= 0; i--)
            {
                OndaFx o = ondas[i];
                o.t += dt;
                float k = Mathf.Clamp01(o.t / o.dur);
                float suave = 1f - (1f - k) * (1f - k);
                GraficosProc.TrazarCirculo(o.lr, o.centro, Mathf.Max(0.05f, o.radio * suave), 40);
                Color c = o.color;
                c.a = o.color.a * (1f - k);
                o.lr.startColor = c;
                o.lr.endColor = c;
                float ancho = Mathf.Lerp(0.14f, 0.03f, k);
                o.lr.startWidth = ancho;
                o.lr.endWidth = ancho;
                if (o.t >= o.dur)
                {
                    DevolverLinea(o.lr);
                    ondas.RemoveAt(i);
                }
            }

            for (int i = rayos.Count - 1; i >= 0; i--)
            {
                RayoFx r = rayos[i];
                r.t += dt;
                float k = Mathf.Clamp01(r.t / r.dur);
                Color c = Color.Lerp(Color.white, r.color, k);
                c.a = 1f - k;
                r.lr.startColor = c;
                r.lr.endColor = c;
                if (r.t >= r.dur)
                {
                    DevolverLinea(r.lr);
                    rayos.RemoveAt(i);
                }
            }

            for (int i = destellos.Count - 1; i >= 0; i--)
            {
                DestelloFx d = destellos[i];
                d.t += dt;
                float k = Mathf.Clamp01(d.t / d.dur);
                d.sr.transform.localScale = Vector3.one * d.tam * Mathf.Lerp(1f, 1.6f, k);
                Color c = d.color;
                c.a = 1f - k;
                d.sr.color = c;
                if (d.t >= d.dur)
                {
                    d.sr.gameObject.SetActive(false);
                    poolDestellos.Push(d.sr);
                    destellos.RemoveAt(i);
                }
            }
        }

        private void LateUpdate()
        {
            if (!sacudiendo || cam == null) return;
            shakeT += Time.unscaledDeltaTime;
            if (shakeT >= shakeDur)
            {
                cam.transform.position = camBase;
                sacudiendo = false;
                return;
            }
            float amort = 1f - shakeT / shakeDur;
            Vector2 off = Random.insideUnitCircle * shakeInt * amort;
            cam.transform.position = camBase + new Vector3(off.x, off.y, 0f);
        }

        #region Sonido Sintetizado

        private void CrearAudio()
        {
            fuenteAudio = gameObject.AddComponent<AudioSource>();
            fuenteAudio.playOnAwake = false;
            fuenteAudio.spatialBlend = 0f;
            fuenteAudio.volume = 0.35f;

            if (FindFirstObjectByType<AudioListener>() == null)
            {
                Camera c = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (c != null) c.gameObject.AddComponent<AudioListener>();
            }

            clips[TipoSonido.Disparo] = Tono("disparo", 880f, 440f, 0.06f, 0.25f, 0f);
            clips[TipoSonido.Canon] = Tono("canon", 220f, 70f, 0.16f, 0.5f, 0.4f);
            clips[TipoSonido.Electrico] = Tono("electrico", 1400f, 500f, 0.12f, 0.3f, 0.6f);
            clips[TipoSonido.Explosion] = Tono("explosion", 160f, 40f, 0.3f, 0.6f, 0.85f);
            clips[TipoSonido.Baja] = Tono("baja", 520f, 180f, 0.1f, 0.3f, 0.2f);
            clips[TipoSonido.Construir] = Tono("construir", 330f, 660f, 0.14f, 0.45f, 0f);
            clips[TipoSonido.Mejora] = Tono("mejora", 520f, 1040f, 0.22f, 0.45f, 0f);
            clips[TipoSonido.Oleada] = Tono("oleada", 392f, 784f, 0.4f, 0.5f, 0f);
            clips[TipoSonido.Victoria] = Tono("victoria", 523f, 1046f, 0.8f, 0.55f, 0f);
            clips[TipoSonido.Derrota] = Tono("derrota", 300f, 80f, 0.8f, 0.55f, 0.15f);
            clips[TipoSonido.Error] = Tono("error", 180f, 150f, 0.14f, 0.4f, 0.1f);
            clips[TipoSonido.Habilidad] = Tono("habilidad", 300f, 1200f, 0.35f, 0.5f, 0.1f);
            clips[TipoSonido.Click] = Tono("click", 700f, 700f, 0.03f, 0.25f, 0f);
        }

        /// <summary>Sintetiza un barrido de frecuencia con envolvente decreciente y mezcla opcional de ruido.</summary>
        private static AudioClip Tono(string nombre, float f0, float f1, float duracion, float volumen, float ruido)
        {
            const int frecuencia = 22050;
            int n = Mathf.Max(1, Mathf.RoundToInt(frecuencia * duracion));
            float[] datos = new float[n];
            float fase = 0f;
            System.Random rng = new System.Random(nombre.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float f = Mathf.Lerp(f0, f1, k);
                fase += 2f * Mathf.PI * f / frecuencia;
                float onda = Mathf.Sin(fase);
                float r = (float)(rng.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Min(1f, k * 30f) * (1f - k) * (1f - k);
                datos[i] = Mathf.Lerp(onda, r, ruido) * env * volumen;
            }
            AudioClip clip = AudioClip.Create(nombre, n, 1, frecuencia, false);
            clip.SetData(datos, 0);
            return clip;
        }

        public void Sonido(TipoSonido tipo)
        {
            if (!sonidoActivo || fuenteAudio == null) return;
            if (!clips.TryGetValue(tipo, out AudioClip clip) || clip == null) return;

            // Limitar repeticiones para no saturar con muchas torres disparando
            float ahora = Time.unscaledTime;
            float minimo = (tipo == TipoSonido.Disparo || tipo == TipoSonido.Baja) ? 0.07f : 0.04f;
            if (ultimoSonido.TryGetValue(tipo, out float ultimo) && ahora - ultimo < minimo) return;
            ultimoSonido[tipo] = ahora;
            fuenteAudio.PlayOneShot(clip);
        }

        #endregion
    }
}
