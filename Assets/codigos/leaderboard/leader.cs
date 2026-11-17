using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class EntradaLeaderboard
{
    public string nombre;
    public int puntaje;

    // Campos añadidos (los archivos antiguos cargan con valores por defecto)
    public int nivel = 1;
    public int oleada = 0;
    public bool victoria = false;
    public string clase = "";
    public string fecha = "";
    public string modo = "Solo";

    public EntradaLeaderboard(string nombre, int puntaje)
    {
        this.nombre = nombre;
        this.puntaje = puntaje;
    }
}

[System.Serializable]
public class DatosLeaderboard
{
    public List<EntradaLeaderboard> listaPuntajes = new List<EntradaLeaderboard>();
}

/// <summary>
/// Leaderboard persistente (Top 10) en Application.persistentDataPath/leaderboard.json.
/// Se conecta al fin de partida (Victoria / GameOver) y expone el modal "🏆 Ranking".
/// </summary>
public class LeaderboardJSON : MonoBehaviour
{
    public static LeaderboardJSON Instance { get; private set; }

    public List<EntradaLeaderboard> tablaPuntajes = new List<EntradaLeaderboard>();
    public const int MAXIMO = 10;

    private string rutaArchivo;
    private bool cargado = false;
    private GameObject panel;
    private Text textoTabla;
    private EntradaLeaderboard ultimaEntrada;

    // ── Acceso estático (lo usa la pantalla de fin de partida LAN) ──

    /// <summary>Top 10 guardado en disco (lista vacía si no existe).</summary>
    public static List<EntradaLeaderboard> CargarTop()
    {
        return Asegurar().tablaPuntajes;
    }

    /// <summary>Registra un puntaje de una partida multijugador LAN y devuelve el Top 10 actualizado.</summary>
    public static List<EntradaLeaderboard> AgregarPuntajeGlobal(string nombre, int puntaje, bool victoria = false, int rondas = 0)
    {
        LeaderboardJSON lb = Asegurar();
        lb.RegistrarResultado(nombre, puntaje, 1, rondas, victoria, "", "LAN");
        return lb.tablaPuntajes;
    }

    public static LeaderboardJSON Asegurar()
    {
        if (Instance != null) return Instance;
        LeaderboardJSON existente = FindFirstObjectByType<LeaderboardJSON>();
        if (existente != null)
        {
            existente.Inicializar();
            return existente;
        }
        GameObject go = new GameObject("LeaderboardJSON");
        LeaderboardJSON lb = go.AddComponent<LeaderboardJSON>();
        lb.Inicializar();
        return lb;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Inicializar();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Inicializar()
    {
        Instance = this;
        if (cargado) return;
        rutaArchivo = Path.Combine(Application.persistentDataPath, "leaderboard.json");
        CargarLeaderboard();
        cargado = true;
    }

    public void AgregarPuntaje(string nombre, int puntaje)
    {
        RegistrarResultado(nombre, puntaje, 1, 0, false, "");
    }

    /// <summary>
    /// Inserta un resultado, mantiene la tabla ordenada de mayor a menor y la recorta al Top 10.
    /// Devuelve la posición obtenida (1..10) o -1 si no entró al ranking.
    /// </summary>
    public int RegistrarResultado(string nombre, int puntaje, int nivel, int oleada, bool victoria, string clase, string modo = "Solo")
    {
        Inicializar();
        EntradaLeaderboard entrada = new EntradaLeaderboard(string.IsNullOrEmpty(nombre) ? "Anónimo" : nombre, puntaje)
        {
            nivel = nivel,
            oleada = oleada,
            victoria = victoria,
            clase = clase ?? "",
            modo = modo,
            fecha = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };

        tablaPuntajes.Add(entrada);
        tablaPuntajes.Sort((x, y) => y.puntaje.CompareTo(x.puntaje));
        while (tablaPuntajes.Count > MAXIMO) tablaPuntajes.RemoveAt(tablaPuntajes.Count - 1);

        ultimaEntrada = entrada;
        GuardarLeaderboard();
        RefrescarTabla();
        int pos = tablaPuntajes.IndexOf(entrada);
        return pos >= 0 ? pos + 1 : -1;
    }

    public void GuardarLeaderboard()
    {
        try
        {
            DatosLeaderboard datos = new DatosLeaderboard { listaPuntajes = tablaPuntajes };
            File.WriteAllText(rutaArchivo, JsonUtility.ToJson(datos, true));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Leaderboard] No se pudo guardar: " + ex.Message);
        }
    }

    public void CargarLeaderboard()
    {
        try
        {
            if (!File.Exists(rutaArchivo)) return;
            string json = File.ReadAllText(rutaArchivo);
            if (string.IsNullOrWhiteSpace(json)) return;

            DatosLeaderboard datos = JsonUtility.FromJson<DatosLeaderboard>(json);
            if (datos != null && datos.listaPuntajes != null)
            {
                tablaPuntajes = datos.listaPuntajes;
                tablaPuntajes.RemoveAll(e => e == null);
                tablaPuntajes.Sort((x, y) => y.puntaje.CompareTo(x.puntaje));
            }
        }
        catch (System.Exception ex)
        {
            // Un archivo dañado no debe impedir jugar: se empieza con una tabla vacía
            Debug.LogWarning("[Leaderboard] Archivo ilegible, se inicia vacío: " + ex.Message);
            tablaPuntajes = new List<EntradaLeaderboard>();
        }
    }

    #region Modal 🏆 Ranking

    public void AlternarPanel()
    {
        if (panel == null) ConstruirPanel();
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) RefrescarTabla();
    }

    public void MostrarPanel()
    {
        if (panel == null) ConstruirPanel();
        panel.SetActive(true);
        RefrescarTabla();
    }

    private void ConstruirPanel()
    {
        GameObject canvas = UIFabrica.CrearCanvas("Canvas_Leaderboard", 108);
        canvas.transform.SetParent(transform, false);

        RectTransform rt = UIFabrica.CrearPanel(canvas.transform, "PanelRanking", Paleta.FondoPanel, new Color(1f, 0.82f, 0.25f, 0.7f));
        UIFabrica.Anclar(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 560));
        panel = rt.gameObject;

        UIFabrica.CrearEncabezado(rt, "🏆 RANKING DE COMANDANTES — TOP 10", Paleta.Dorado, () => panel.SetActive(false));

        Text sub = UIFabrica.CrearTexto(rt, "Sub", "Puntaje = bajas + oleadas superadas + vida de la base + oro restante (+ bono de victoria) × dificultad",
            12, TextAnchor.MiddleLeft, new Color(0.65f, 0.75f, 0.88f));
        UIFabrica.Colocar(sub, 18, -52, 724, 22);

        RectTransform fondo = UIFabrica.CrearPanel(rt, "FondoTabla", Paleta.FondoOscuro);
        UIFabrica.Colocar(fondo, 18, -80, 724, 420);
        textoTabla = UIFabrica.CrearTexto(fondo, "Tabla", "", 15, TextAnchor.UpperLeft, Color.white);
        UIFabrica.Estirar(textoTabla, 14, 10, 14, 10);

        Button limpiar = UIFabrica.CrearBoton(rt, "BtnLimpiar", "🗑️ Vaciar ranking", new Color(0.5f, 0.2f, 0.2f));
        UIFabrica.Colocar(limpiar, 18, -510, 170, 34);
        limpiar.onClick.AddListener(() =>
        {
            tablaPuntajes.Clear();
            ultimaEntrada = null;
            GuardarLeaderboard();
            RefrescarTabla();
        });

        Text ruta = UIFabrica.CrearTexto(rt, "Ruta", "Persistencia: leaderboard.json (JSON)", 11, TextAnchor.MiddleRight, new Color(0.5f, 0.6f, 0.7f));
        UIFabrica.Colocar(ruta, 200, -510, 542, 34);

        panel.SetActive(false);
    }

    private void RefrescarTabla()
    {
        if (textoTabla == null) return;
        if (tablaPuntajes.Count == 0)
        {
            textoTabla.text = "<color=#94a3b8><i>Aún no hay partidas registradas.\nTermina una partida (victoria o derrota) para aparecer aquí.</i></color>";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<color=#66ddff><b> #    Comandante                 Puntaje    Nivel   Oleada   Resultado</b></color>");
        sb.AppendLine();
        for (int i = 0; i < tablaPuntajes.Count; i++)
        {
            EntradaLeaderboard e = tablaPuntajes[i];
            string medalla = i == 0 ? "🥇" : (i == 1 ? "🥈" : (i == 2 ? "🥉" : $"{i + 1:00}"));
            string color = e == ultimaEntrada ? "#44ff88" : (i < 3 ? "#ffd700" : "#e2e8f0");
            string resultado = e.victoria ? "👑 Victoria" : (e.oleada > 0 ? "💀 Derrota" : "—");
            string clase = string.IsNullOrEmpty(e.clase) ? "" : $" <color=#94a3b8>({e.clase})</color>";
            string nuevo = e == ultimaEntrada ? "  <color=#44ff88><b>◀ NUEVO</b></color>" : "";
            sb.AppendLine($"<color={color}><b>{medalla}</b>   <b>{e.nombre}</b></color>{clase}   —   <color=#ffd700><b>{e.puntaje}</b> pts</color>   ·   Nvl {e.nivel}   ·   Oleada {e.oleada}   ·   {resultado}   <color=#64748b>{e.modo} {e.fecha}</color>{nuevo}");
            sb.AppendLine();
        }
        textoTabla.text = sb.ToString();
    }

    #endregion
}
