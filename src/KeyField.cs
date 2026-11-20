using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Campo para elegir una combinación: se puede escribir ("Ctrl+Alt+T") o pulsar
    // "Grabar" y hacerla con el teclado. Debajo dice si es válida y cómo se verá.
    internal sealed class KeyField : UserControl
    {
        private const int RecordTimeoutMs = 10000;

        private readonly Theme _theme;
        private readonly bool _forHotkey;
        private readonly TextBox _text;
        private readonly Button _record;
        private readonly Label _status;
        private readonly KeyRecorder _recorder = new KeyRecorder();
        private readonly Timer _timeout = new Timer();

        public event EventHandler ValueChanged;

        // forHotkey: la tecla que abre AutoShortcut, que debe llevar Win, Ctrl o Alt.
        public KeyField(Theme theme, int width, bool forHotkey, string accessibleName)
        {
            _theme = theme;
            _forHotkey = forHotkey;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = theme.Background;

            InputBox input = new InputBox(theme, width - Ui.S(104), accessibleName);
            input.Location = Point.Empty;
            _text = input.Box;
            _text.TextChanged += delegate
            {
                ShowStatus();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            };

            _record = new Button();
            _record.Text = "Grabar";
            Ui.StyleButton(_record, theme, false);
            _record.Bounds = new Rectangle(width - Ui.S(96), 0, Ui.S(96), input.Height);
            _record.Click += delegate
            {
                if (_recorder.Active) StopRecording(); else StartRecording();
            };

            _status = new Label();
            _status.AutoSize = false;
            _status.Font = new Font("Segoe UI", 8.25F);
            _status.Bounds = new Rectangle(0, input.Height + Ui.S(4), width, Ui.S(20));

            Controls.Add(input);
            Controls.Add(_record);
            Controls.Add(_status);
            Size = new Size(width, _status.Bottom);

            // El hook llama desde dentro del procesado de teclas: dejamos que termine
            // y actualizamos la interfaz justo después (BeginInvoke).
            _recorder.Recorded += delegate(KeyCombo combo)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    StopRecording();
                    Value = combo.ToString();
                    _text.Focus();
                    _text.SelectAll();
                });
            };
            _recorder.Cancelled += delegate { BeginInvoke((MethodInvoker)StopRecording); };
            _timeout.Interval = RecordTimeoutMs;
            _timeout.Tick += delegate { StopRecording(); };

            ShowStatus();
        }

        public string Value
        {
            get { return _text.Text.Trim(); }
            set { _text.Text = value ?? ""; }
        }

        public bool IsValid
        {
            get { string error; return Check(out error) != null; }
        }

        private KeyCombo Check(out string error)
        {
            error = null;
            KeyCombo combo;
            if (Value.Length == 0)
            {
                error = "Escríbela (p. ej. Ctrl+Alt+T) o pulsa Grabar.";
                return null;
            }
            if (!KeyCombo.TryParse(Value, out combo))
            {
                error = "No la entiendo. Ejemplo: Ctrl+Alt+T";
                return null;
            }
            if (_forHotkey && !(combo.Has(KeyCombo.MOD_WIN) || combo.Has(KeyCombo.MOD_CONTROL) || combo.Has(KeyCombo.MOD_ALT)))
            {
                error = "Debe llevar Win, Ctrl o Alt (si no, chocaría con lo que escribes).";
                return null;
            }
            return combo;
        }

        private void ShowStatus()
        {
            if (_recorder.Active) return;
            string error;
            KeyCombo combo = Check(out error);
            if (combo == null)
            {
                _status.ForeColor = Value.Length == 0 ? _theme.Subtle : _theme.Error;
                _status.Text = error;
            }
            else
            {
                _status.ForeColor = _theme.Subtle;
                _status.Text = "✓  " + string.Join(" + ", combo.DisplayParts());
            }
        }

        private void StartRecording()
        {
            if (!_recorder.Start())
            {
                _status.ForeColor = _theme.Error;
                _status.Text = "Windows no ha dejado grabar. Escríbela a mano.";
                return;
            }
            _record.Text = "Parar";
            _status.ForeColor = _theme.Accent;
            _status.Text = "Pulsa la combinación…  (Esc para cancelar)";
            _timeout.Start();
        }

        public void StopRecording()
        {
            _timeout.Stop();
            _recorder.Stop();
            _record.Text = "Grabar";
            ShowStatus();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) StopRecording();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _recorder.Dispose();
                _timeout.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
