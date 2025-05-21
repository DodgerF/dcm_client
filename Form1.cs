using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class Form1 : Form
    {
        private TextBox txtSearch = null!;
        private DataGridView dgvClients = null!;
        private Button btnConfirm = null!;
        private Button btnBack = null!;
        private Button btnAddClient = null!;
        private Button btnUploadResearch = null!;  // кнопка для загрузки архива
        private TextBox txtSelectedName = null!;
        private TextBox txtSelectedPhone = null!;

        private readonly HttpClient _httpClient;
        private int _selectedClientId;

        public Form1()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8080/") };
            InitializeComponent();
            SetupSearchGrid();
        }

        private void InitializeComponent()
        {
            // Search textbox
            txtSearch = new TextBox
            {
                Location = new Point(12, 12),
                Width = 300,
                PlaceholderText = "Введите имя, ID или телефон"
            };
            txtSearch.KeyDown += TxtSearch_KeyDown;

            // Add client button
            btnAddClient = new Button
            {
                Text = "Добавить пациента",
                Location = new Point(txtSearch.Right + 10, txtSearch.Top),
                Size = new Size(120, txtSearch.Height)
            };
            btnAddClient.Click += async (_, __) =>
            {
                using var dlg = new AddClientForm(_httpClient);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await SearchClientsAsync(txtSearch.Text.Trim());
                }
            };

            // Selected name and phone (readonly)
            txtSelectedName = new TextBox
            {
                Location = new Point(12, 12),
                Width = 300,
                ReadOnly = true,
                Visible = false
            };
            txtSelectedPhone = new TextBox
            {
                Location = new Point(330, 12),
                Width = 150,
                ReadOnly = true,
                Visible = false
            };

            // DataGridView
            dgvClients = new DataGridView
            {
                Location = new Point(12, 40),
                Size = new Size(600, 300),
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false
            };
            dgvClients.SelectionChanged += (_, __) => btnConfirm.Enabled = dgvClients.SelectedRows.Count > 0;

            // Confirm button
            btnConfirm = new Button
            {
                Text = "Подтвердить",
                Location = new Point(12, 350),
                Size = new Size(120, 30),
                Enabled = false
            };
            btnConfirm.Click += async (_, __) => await OnConfirmAsync();

            // Upload research button (hidden initially)
            btnUploadResearch = new Button
            {
                Text = "Добавить исследование",
                Location = new Point(140, 350),
                Size = new Size(150, 30),
                Visible = false
            };
            btnUploadResearch.Click += async (_, __) => await OnUploadResearchAsync();

            // Back button
            btnBack = new Button
            {
                Text = "Назад",
                Location = new Point(12, 350),
                Size = btnConfirm.Size,
                Visible = false
            };
            btnBack.Click += (_, __) => OnBack();

            // Add controls
            Controls.Add(txtSearch);
            Controls.Add(btnAddClient);
            Controls.Add(txtSelectedName);
            Controls.Add(txtSelectedPhone);
            Controls.Add(dgvClients);
            Controls.Add(btnConfirm);
            Controls.Add(btnUploadResearch);
            Controls.Add(btnBack);

            // Form settings
            Text = "Поиск клиентов / Исследования";
            ClientSize = new Size(640, 400);
        }

        private void SetupSearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№", Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",   HeaderText = "Полное имя", DataPropertyName = "FullName",   Width = 250 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPhone",  HeaderText = "Телефон",     DataPropertyName = "PhoneNumber", Width = 150 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId",     HeaderText = "ID",           DataPropertyName = "Id",          Visible = false });

            txtSearch.Visible = true;
            btnAddClient.Visible = true;
            txtSelectedName.Visible = false;
            txtSelectedPhone.Visible = false;
            btnConfirm.Visible = true;
            btnUploadResearch.Visible = false;
            btnBack.Visible = false;
        }

        private void SetupResearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№",               Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate",   HeaderText = "Дата исследования", Width = 200 });

            txtSearch.Visible = false;
            btnAddClient.Visible = false;
            txtSelectedName.Visible = true;
            txtSelectedPhone.Visible = true;
            btnConfirm.Visible = false;
            btnUploadResearch.Visible = true;
            btnBack.Visible = true;
        }

        private async void TxtSearch_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await SearchClientsAsync(txtSearch.Text.Trim());
            }
        }

        private async Task SearchClientsAsync(string query)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(query) ? "api/clients" : $"api/clients?q={Uri.EscapeDataString(query)}";
                var clients = await _httpClient.GetFromJsonAsync<List<ClientDto>>(url) ?? new List<ClientDto>();

                dgvClients.Rows.Clear();
                int rowNum = 1;
                foreach (var c in clients)
                    dgvClients.Rows.Add(rowNum++, c.FullName, c.PhoneNumber, c.Id);

                btnConfirm.Enabled = dgvClients.Rows.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке клиентов:\n{ex.Message}", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OnConfirmAsync()
        {
            if (dgvClients.SelectedRows.Count == 0) return;
            var row = dgvClients.SelectedRows[0];
            _selectedClientId = Convert.ToInt32(row.Cells["colId"].Value);
            txtSelectedName.Text  = row.Cells["colName"].Value?.ToString() ?? "";
            txtSelectedPhone.Text = row.Cells["colPhone"].Value?.ToString() ?? "";

            SetupResearchGrid();
            await SearchResearchesAsync(_selectedClientId);
        }

        private async Task OnUploadResearchAsync()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "ZIP archives (*.zip)|*.zip|All files (*.*)|*.*",
                Title = "Выберите ZIP с DICOMDIR"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var filePath = dlg.FileName;
                byte[] bytes = await File.ReadAllBytesAsync(filePath);

                using var content = new MultipartFormDataContent();
                var zipContent = new ByteArrayContent(bytes);
                zipContent.Headers.ContentType = 
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                content.Add(zipContent, "archive", Path.GetFileName(filePath));

                var response = await _httpClient
                    .PostAsync($"api/clients/{_selectedClientId}/researches", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Ошибка сервера {response.StatusCode}:\n{errorText}",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }

                MessageBox.Show(
                    "Исследование успешно загружено.",
                    "Готово",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                // Обновляем таблицу исследований
                await SearchResearchesAsync(_selectedClientId);
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show(
                    $"Сетевая ошибка при загрузке архива:\n{httpEx.Message}",
                    "Ошибка сети",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Неожиданная ошибка:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }


        private async Task SearchResearchesAsync(int clientId)
        {
            try
            {
                var researches = await _httpClient.GetFromJsonAsync<List<ResearchDto>>(
                    $"api/clients/{clientId}/researches"
                ) ?? new List<ResearchDto>();

                dgvClients.Rows.Clear();
                int rowNum = 1;
                foreach (var r in researches)
                    dgvClients.Rows.Add(rowNum++, r.StudyDate.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке исследований:\n{ex.Message}", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBack()
        {
            SetupSearchGrid();
            txtSearch.Text = string.Empty;
            dgvClients.Rows.Clear();
            btnConfirm.Enabled = false;
        }

        // DTO для клиентов
        private class ClientDto
        {
            public int Id { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
        }

        // DTO для исследований
        private class ResearchDto
        {
            public string OrthancId { get; set; } = string.Empty;
            public DateTime StudyDate { get; set; }
        }
    }
}