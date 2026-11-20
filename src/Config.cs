using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AutoShortcut
{
    // La configuración vive en %APPDATA%\AutoShortcut\shortcuts.json:
    //
    //   {
    //     "hotkey": "Win+Shift+A",
    //     "shortcuts": [
    //       { "name": "Capturar una zona", "keys": "Win+Shift+S" },
    //       { "name": "Bloc de notas", "run": "notepad.exe" }
    //     ]
    //   }
    //
    // Para leer usamos JavaScriptSerializer, que viene con .NET Framework
    // (System.Web.Extensions.dll). Para escribir generamos el texto a mano:
    // así queda con sangría y un atajo por línea, cómodo de editar.
    internal sealed class Config
    {
        public const string DefaultHotkey = "Win+Shift+A";

        public string Hotkey = DefaultHotkey;
        public List<Shortcut> Shortcuts = new List<Shortcut>();

        public static string Folder
        {
            get
            {
                string appData = Environment.GetEnvironmentVariable("APPDATA");
                if (string.IsNullOrEmpty(appData))
                    appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "AutoShortcut");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "shortcuts.json"); }
        }

        // La primera vez crea el archivo con unos atajos de ejemplo.
        // Si el archivo tiene un error lanza FormatException y NO lo toca.
        public static Config Load()
        {
            if (!File.Exists(FilePath))
            {
                Config fresh = CreateDefault();
                fresh.Save();
                return fresh;
            }
            return Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            // Escribir en un temporal y luego reemplazar: si algo falla a mitad,
            // el archivo bueno sigue ahí.
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, ToJson(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        public static Config CreateDefault()
        {
            Config c = new Config();
            c.AddKeys("Capturar una zona de la pantalla", "Win+Shift+S");
            c.AddKeys("Historial del portapapeles", "Win+V");
            c.AddKeys("Emojis y símbolos", "Win+.");
            c.AddKeys("Explorador de archivos", "Win+E");
            c.AddKeys("Mostrar el escritorio", "Win+D");
            c.AddKeys("Configuración de Windows", "Win+I");
            c.AddKeys("Administrador de tareas", "Ctrl+Shift+Escape");
            c.AddRun("Bloc de notas", "notepad.exe");
            c.AddRun("Calculadora", "calc.exe");
            c.AddRun("Carpeta Descargas", "shell:Downloads");
            c.AddRun("GitHub", "https://github.com");
            return c;
        }

        private void AddKeys(string name, string keys)
        {
            Shortcut s = new Shortcut();
            s.Name = name;
            s.KeysText = keys;
            Shortcuts.Add(s);
        }

        private void AddRun(string name, string run)
        {
            Shortcut s = new Shortcut();
            s.Name = name;
            s.Run = run;
            Shortcuts.Add(s);
        }

        // ---- Lectura ----

        public static Config Parse(string json)
        {
            object root;
            try
            {
                root = new JavaScriptSerializer().DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new FormatException("el JSON no es válido (" + ex.Message + ")");
            }

            IDictionary<string, object> obj = root as IDictionary<string, object>;
            if (obj == null) throw new FormatException("debe empezar por { y terminar por }");

            Config c = new Config();
            string hotkey = GetString(obj, "hotkey");
            if (!string.IsNullOrEmpty(hotkey)) c.Hotkey = hotkey;

            object list;
            if (obj.TryGetValue("shortcuts", out list) && list != null)
            {
                IList items = list as IList;
                if (items == null) throw new FormatException("\"shortcuts\" debe ser una lista [ ... ]");
                foreach (object item in items)
                {
                    IDictionary<string, object> entry = item as IDictionary<string, object>;
                    if (entry == null) continue;

                    Shortcut s = new Shortcut();
                    s.Name = GetString(entry, "name");
                    s.KeysText = GetString(entry, "keys");
                    s.Run = GetString(entry, "run");
                    s.Args = GetString(entry, "args");
                    if (string.IsNullOrEmpty(s.Name)) s.Name = s.IsLaunch ? s.Run : s.KeysText;
                    if (string.IsNullOrEmpty(s.Name)) continue;   // entrada vacía
                    c.Shortcuts.Add(s);
                }
            }
            return c;
        }

        private static string GetString(IDictionary<string, object> obj, string key)
        {
            object value;
            if (!obj.TryGetValue(key, out value) || value == null) return null;
            string s = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture).Trim();
            return s.Length == 0 ? null : s;
        }

        // ---- Escritura ----

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\r\n");
            sb.Append("  \"hotkey\": ").Append(Quote(Hotkey)).Append(",\r\n");
            sb.Append("  \"shortcuts\": [");
            for (int i = 0; i < Shortcuts.Count; i++)
            {
                Shortcut s = Shortcuts[i];
                sb.Append(i == 0 ? "\r\n" : ",\r\n");
                sb.Append("    { \"name\": ").Append(Quote(s.Name));
                if (s.IsLaunch)
                {
                    sb.Append(", \"run\": ").Append(Quote(s.Run));
                    if (!string.IsNullOrEmpty(s.Args)) sb.Append(", \"args\": ").Append(Quote(s.Args));
                }
                else
                {
                    sb.Append(", \"keys\": ").Append(Quote(s.KeysText));
                }
                sb.Append(" }");
            }
            sb.Append(Shortcuts.Count == 0 ? "]\r\n" : "\r\n  ]\r\n");
            sb.Append("}\r\n");
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
