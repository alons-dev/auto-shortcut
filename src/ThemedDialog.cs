using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoShortcut
{
    // Base de las ventanas de diálogo (editor y configuración): marco de Windows 11
    // con barra de título clara u oscura, colores del tema y botones Guardar/Cancelar.
    // Las medidas se escriben a 100 % y se escalan con Ui.S.
    internal class ThemedDialog : Form
    {
        protected readonly Theme T;
        protected readonly Label ErrorLabel;
        protected readonly Button SaveButton;
        protected readonly Button CancelBtn;

        protected ThemedDialog(Theme theme, string title, int width, int height)
        {
            T = theme;
            AutoScaleMode = AutoScaleMode.None;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;   // la app no tiene ventana principal: que se pueda encontrar
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Font = new Font("Segoe UI", 9.75F);
            BackColor = theme.Background;
            ForeColor = theme.Foreground;
            ClientSize = new Size(Ui.S(width), Ui.S(height));

            int buttonsY = height - 44;
            ErrorLabel = new Label();
            ErrorLabel.ForeColor = theme.Error;
            ErrorLabel.AutoSize = false;
            ErrorLabel.Bounds = new Rectangle(Ui.S(16), Ui.S(buttonsY - 2), Ui.S(width - 16 - 220), Ui.S(36));
            Controls.Add(ErrorLabel);

            CancelBtn = AddButton("Cancelar", width - 16 - 96 - 8 - 96, buttonsY, 96, false);
            CancelBtn.DialogResult = DialogResult.Cancel;
            SaveButton = AddButton("Guardar", width - 16 - 96, buttonsY, 96, true);
            SaveButton.Click += delegate
            {
                string error = TrySave();
                if (error == null) { DialogResult = DialogResult.OK; Close(); }
                else ErrorLabel.Text = error;
            };
            AcceptButton = SaveButton;
            CancelButton = CancelBtn;
        }

        // Valida y guarda. Devuelve null si todo bien o el error a mostrar.
        protected virtual string TrySave()
        {
            return null;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.ApplyFrame(Handle, T.Dark, Ui.CornerDefault);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        // ---- Ayudas para colocar controles (coordenadas a 100 %) ----

        protected Label AddLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(Ui.S(x), Ui.S(y) + Ui.S(6));   // centrada con un cuadro de 32 px
            Controls.Add(l);
            return l;
        }

        protected Label AddNote(string text, int x, int y, int width)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = T.Subtle;
            l.Font = new Font("Segoe UI", 8.25F);
            l.AutoSize = false;
            l.Bounds = new Rectangle(Ui.S(x), Ui.S(y), Ui.S(width), Ui.S(20));
            Controls.Add(l);
            return l;
        }

        protected TextBox AddTextBox(int x, int y, int width, string accessibleName)
        {
            InputBox input = new InputBox(T, Ui.S(width), accessibleName);
            input.Location = new Point(Ui.S(x), Ui.S(y));
            Controls.Add(input);
            return input.Box;
        }

        protected Button AddButton(string text, int x, int y, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            Ui.StyleButton(b, T, primary);
            b.Bounds = new Rectangle(Ui.S(x), Ui.S(y), Ui.S(width), Ui.S(32));
            Controls.Add(b);
            return b;
        }
    }
}
