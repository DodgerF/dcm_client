using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client;
public class AddClientForm : Form
{
    private TextBox txtFullName = null!;
    private TextBox txtPhone = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;
    private readonly HttpClient _httpClient;

    public AddClientForm(HttpClient httpClient)
    {
        _httpClient = httpClient;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Новый пациент";
        ClientSize = new Size(360, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var lblName = new Label { Text = "ФИО:", Location = new Point(10, 15), AutoSize = true };
        txtFullName = new TextBox { Location = new Point(80, 12), Width = 260 };


        var lblPhone = new Label { Text = "Телефон:", Location = new Point(10, 50), AutoSize = true };
        txtPhone = new TextBox { Location = new Point(80, 47), Width = 260 };


        btnSave = new Button
        {
            Text = "Сохранить",
            Location = new Point(80, 85),
            DialogResult = DialogResult.None
        };
        btnSave.Click += async (_, __) => await SaveAsync();

        btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(180, 85),
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(lblName);
        Controls.Add(txtFullName);
        Controls.Add(lblPhone);
        Controls.Add(txtPhone);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(txtFullName.Text) ||
            string.IsNullOrWhiteSpace(txtPhone.Text))
        {
            MessageBox.Show("Заполните оба поля.", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var req = new 
            {
                fullName    = txtFullName.Text.Trim(),
                phoneNumber = txtPhone.Text.Trim()
            };
            var resp = await _httpClient
                .PostAsJsonAsync("api/clients", req);

            if (resp.IsSuccessStatusCode)
            {
                MessageBox.Show("Пациент успешно добавлен.", "OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                var error = await resp.Content.ReadAsStringAsync();
                MessageBox.Show($"Ошибка сервера: {error}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось отправить запрос:\n{ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
