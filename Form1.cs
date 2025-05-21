using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
     public class Form1 : Form
    {
        private TextBox txtSearch = null!;
        private DataGridView dgvClients = null!;
        private Button btnConfirm = null!;
        private Button btnAddClient = null!;
        private Button btnBack = null!;
        private Button btnUploadResearch = null!;
        private Button btnDisplay = null!;
        private TextBox txtSelectedName = null!;
        private TextBox txtSelectedPhone = null!;
        private PictureBox picPreview = null!;
        private TrackBar trackImages = null!;
        private List<string> _imageUrls = new();

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
            txtSearch = new TextBox {
                Location = new Point(12, 12),
                Width = 300,
                PlaceholderText = "Введите имя, ID или телефон"
            };
            txtSearch.KeyDown += TxtSearch_KeyDown;

            btnAddClient = new Button {
                Text = "Добавить пациента",
                Location = new Point(txtSearch.Right + 10, txtSearch.Top),
                Size = new Size(120, txtSearch.Height)
            };
            btnAddClient.Click += async (_, __) => {
                using var dlg = new AddClientForm(_httpClient);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    await SearchClientsAsync(txtSearch.Text.Trim());
            };

            txtSelectedName = new TextBox { Location = new Point(12, 12), Width = 300, ReadOnly = true, Visible = false };
            txtSelectedPhone = new TextBox { Location = new Point(330, 12), Width = 150, ReadOnly = true, Visible = false };

            dgvClients = new DataGridView {
                Location = new Point(12, 40),
                Size = new Size(600, 300),
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false
            };
            dgvClients.SelectionChanged += (_, __) => {
                if (btnConfirm.Visible) {
                    btnConfirm.Enabled = dgvClients.SelectedRows.Count > 0;
                } else {
                    bool has = dgvClients.SelectedRows.Count > 0;
                    btnDisplay.Visible = has;
                    btnDisplay.Enabled = has;
                }
            };

            btnConfirm = new Button {
                Text = "Подтвердить",
                Location = new Point(12, 350),
                Size = new Size(120, 30),
                Enabled = false
            };
            btnConfirm.Click += async (_, __) => await OnConfirmAsync();

            btnUploadResearch = new Button {
                Text = "Добавить исследование",
                Size = new Size(150, 30)
            };
            btnUploadResearch.Click += async (_, __) => await OnUploadResearchAsync();

            btnBack = new Button {
                Text = "Назад",
                Size = new Size(120, 30)
            };
            btnBack.Click += (_, __) => OnBack();

            btnDisplay = new Button {
                Text = "Отобразить",
                Size = new Size(120, 30),
                Visible = false,
                Enabled = false
            };
            btnDisplay.Click += async (_, __) => await OnDisplayAsync();

            picPreview = new PictureBox {
                Size = new Size(350, 300),
                Location = new Point(dgvClients.Right + 10, dgvClients.Top),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            trackImages = new TrackBar {
                Size = new Size(picPreview.Width, 45),
                Location = new Point(picPreview.Left, picPreview.Bottom + 5),
                Minimum = 0,
                TickStyle = TickStyle.None,
                Visible = false
            };
            trackImages.Scroll += async (_, __) => {
                int idx = trackImages.Value;
                if (idx >= 0 && idx < _imageUrls.Count)
                    await LoadImageAsync(_imageUrls[idx]);
            };

            Controls.AddRange(new Control[] {
                txtSearch, btnAddClient,
                txtSelectedName, txtSelectedPhone,
                dgvClients,
                btnConfirm, btnUploadResearch, btnBack, btnDisplay,
                picPreview, trackImages
            });

            Text = "Поиск клиентов / Исследования";
            ClientSize = new Size(1000, 480);
        }

        private void SetupSearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№", Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Полное имя", DataPropertyName = "FullName", Width = 250 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPhone", HeaderText = "Телефон", DataPropertyName = "PhoneNumber", Width = 150 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "ID", DataPropertyName = "Id", Visible = false });

            txtSearch.Visible         = true;
            btnAddClient.Visible      = true;
            btnConfirm.Visible        = true;

            txtSelectedName.Visible   = false;
            txtSelectedPhone.Visible  = false;
            btnUploadResearch.Visible = false;
            btnBack.Visible           = false;
            btnDisplay.Visible        = false;
            picPreview.Visible        = false;
            trackImages.Visible       = false;

            btnConfirm.Location          = new Point(12, 350);
            btnUploadResearch.Location   = new Point(140, 350);
            btnBack.Location             = new Point(12, 350);
            btnDisplay.Location          = new Point(btnUploadResearch.Right + 10, btnUploadResearch.Top);
        }

        private void SetupResearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№", Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate", HeaderText = "Дата исследования", DataPropertyName = "StudyDate", Width = 200 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrthancId", HeaderText = "OrthancId", DataPropertyName = "OrthancId", Visible = false });

            txtSearch.Visible         = false;
            btnAddClient.Visible      = false;
            btnConfirm.Visible        = false;

            txtSelectedName.Visible   = true;
            txtSelectedPhone.Visible  = true;
            btnUploadResearch.Visible = true;
            btnBack.Visible           = true;
            btnDisplay.Visible        = false;
            btnDisplay.Enabled        = false;

            picPreview.Visible        = false;
            trackImages.Visible       = false;

            btnBack.Location           = new Point(12, 350);
            btnUploadResearch.Location = new Point(140, 350);
            btnDisplay.Location        = new Point(btnUploadResearch.Right + 10, btnUploadResearch.Top);
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
            var url = string.IsNullOrWhiteSpace(query)
                ? "api/clients"
                : $"api/clients?q={Uri.EscapeDataString(query)}";
            var clients = await _httpClient.GetFromJsonAsync<List<ClientDto>>(url)
                          ?? new List<ClientDto>();

            dgvClients.Rows.Clear();
            int i = 1;
            foreach (var c in clients)
                dgvClients.Rows.Add(i++, c.FullName, c.PhoneNumber, c.Id);

            btnConfirm.Enabled = dgvClients.Rows.Count > 0;
        }

        private async Task OnConfirmAsync()
        {
            if (dgvClients.SelectedRows.Count == 0) return;

            var row = dgvClients.SelectedRows[0];
            _selectedClientId     = Convert.ToInt32(row.Cells["colId"].Value);
            txtSelectedName.Text  = row.Cells["colName"].Value?.ToString()  ?? "";
            txtSelectedPhone.Text = row.Cells["colPhone"].Value?.ToString() ?? "";

            SetupResearchGrid();
            await SearchResearchesAsync(_selectedClientId);
        }

        private async Task SearchResearchesAsync(int clientId)
        {
            var researches = await _httpClient
                .GetFromJsonAsync<List<ResearchDto>>($"api/clients/{clientId}/researches")
                ?? new List<ResearchDto>();

            dgvClients.Rows.Clear();
            int i = 1;
            foreach (var r in researches)
                dgvClients.Rows.Add(i++, r.StudyDate.ToString("yyyy-MM-dd"), r.OrthancId);
        }

        private async Task OnDisplayAsync()
        {
            if (dgvClients.SelectedRows.Count == 0)
                return;

            string orthancId = dgvClients.SelectedRows[0]
                .Cells["colOrthancId"].Value!.ToString()!;

            string url = $"api/clients/{_selectedClientId}/researches/{orthancId}/images";

            _imageUrls = await _httpClient
                .GetFromJsonAsync<List<string>>(url)
                ?? new List<string>();

            if (_imageUrls.Count == 0)
            {
                MessageBox.Show("Превью не найдены", "Внимание",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            trackImages.Minimum = 0;
            trackImages.Maximum = _imageUrls.Count - 1;
            trackImages.Value   = 0;
            trackImages.Visible = true;
            picPreview.Visible  = true;

            await LoadImageAsync(_imageUrls[0]);
        }

        private async Task LoadImageAsync(string url)
        {
            using var stream = await _httpClient.GetStreamAsync(url);
            var img = Image.FromStream(stream);
            picPreview.Image?.Dispose();
            picPreview.Image = img;
        }

        private async Task OnUploadResearchAsync()
        {
            using var dlg = new OpenFileDialog {
                Filter = "ZIP archives (*.zip)|*.zip|All files (*.*)|*.*",
                Title  = "Выберите ZIP с DICOMDIR"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            byte[] bytes = await File.ReadAllBytesAsync(dlg.FileName);
            using var content = new MultipartFormDataContent();
            var zipContent = new ByteArrayContent(bytes);
            zipContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(zipContent, "archive", Path.GetFileName(dlg.FileName));

            var resp = await _httpClient
                .PostAsync($"api/clients/{_selectedClientId}/researches", content);
            resp.EnsureSuccessStatusCode();

            await SearchResearchesAsync(_selectedClientId);
        }

        private void OnBack()
        {
            SetupSearchGrid();
            txtSearch.Text = "";
            dgvClients.Rows.Clear();
            btnConfirm.Enabled  = false;
        }

    }
}
