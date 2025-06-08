using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class StartForm : Form
    {
        private TextBox txtSearch = null!;
        private DataGridView dgvClients = null!;
        private Button btnConfirm = null!;
        private Button btnAddClient = null!;
        private Button btnEditClient = null!;
        private Button btnUploadStudy = null!;
        private Button btnDisplay = null!;
        private Button btnBack = null!;
        private TextBox txtSelectedName = null!;
        private TextBox txtSelectedPolicy = null!;

        private readonly HttpClient _httpClient;
        private int _selectedClientId;
        private string _selectedStudyId = null!;

        private readonly string _userRole;

        public StartForm(string jwtToken, string userRole)
        {
            _userRole = userRole;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost:8444/")
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwtToken);

            InitializeComponent();
            SetupSearchGrid();

            if (_userRole != "ROLE_DOCTOR")
            {
                btnConfirm.Visible = false;
            }
        }

        private void InitializeComponent()
        {
            txtSearch = new TextBox
            {
                Location = new Point(12, 12),
                Width = 300,
                PlaceholderText = "Введите имя, ID или полис"
            };
            txtSearch.KeyDown += TxtSearch_KeyDown;

            btnAddClient = new Button
            {
                Text = "Добавить пациента",
                Location = new Point(txtSearch.Right + 10, txtSearch.Top),
                Size = new Size(130, txtSearch.Height)
            };
            btnAddClient.Click += async (_, __) =>
            {
                using var dlg = new AddClientForm(_httpClient);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    await SearchClientsAsync(txtSearch.Text.Trim());
            };

            btnEditClient = new Button
            {
                Text = "Редактировать пациента",
                Location = new Point(btnAddClient.Right + 10, txtSearch.Top),
                Size = new Size(150, txtSearch.Height),
                Enabled = false
            };
            btnEditClient.Click += async (_, __) => await OnEditClientAsync();

            txtSelectedName = new TextBox
            {
                Location = new Point(12, 12),
                Width = 300,
                ReadOnly = true,
                Visible = false
            };
            txtSelectedPolicy = new TextBox
            {
                Location = new Point(330, 12),
                Width = 150,
                ReadOnly = true,
                Visible = false
            };

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
            dgvClients.SelectionChanged += (_, __) => UpdateButtonsState();

            btnConfirm = new Button
            {
                Text = "Подтвердить",
                Location = new Point(172, 350),
                Size = new Size(120, 30),
                Enabled = false
            };
            btnConfirm.Click += async (_, __) => await OnConfirmAsync();

            btnUploadStudy = new Button
            {
                Text = "Добавить исследование",
                Location = new Point(12, 350),
                Size = new Size(150, 30),
                Visible = false,
                Enabled = false
            };
            btnUploadStudy.Click += async (_, __) => await OnUploadStudyAsync();

            btnDisplay = new Button
            {
                Location = new Point(172, 350),
                Size = new Size(120, 30),
                Visible = false,
                Enabled = false
            };

            btnBack = new Button
            {
                Text = "Назад",
                Location = new Point(12, 350),
                Size = new Size(120, 30),
                Visible = false
            };
            btnBack.Click += (_, __) => OnBack();

            Controls.AddRange(new Control[]
            {
                txtSearch, btnAddClient, btnEditClient,
                txtSelectedName, txtSelectedPolicy,
                dgvClients,
                btnConfirm, btnUploadStudy, btnDisplay, btnBack
            });

            Text = "";
            ClientSize = new Size(800, 420);
        }

        private void UpdateButtonsState()
        {
            bool has = dgvClients.SelectedRows.Count > 0;

            if (btnConfirm.Visible)
                btnConfirm.Enabled = has;

            if (btnUploadStudy.Visible)
                btnUploadStudy.Enabled = has;

            btnEditClient.Enabled = has;

            if (btnDisplay.Visible)
                btnDisplay.Enabled = has;
        }

        private void SetupSearchGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№", Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "ФИО", DataPropertyName = "FullName", Width = 250 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPolicy", HeaderText = "Полис", DataPropertyName = "MedPolicy", Width = 150 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "ID", DataPropertyName = "Id", Visible = false });

            txtSearch.Visible = true;
            btnAddClient.Visible = true;
            btnEditClient.Visible = true;
            btnUploadStudy.Visible = true;
            btnConfirm.Visible = (_userRole == "ROLE_DOCTOR");

            txtSelectedName.Visible = false;
            txtSelectedPolicy.Visible = false;
            btnDisplay.Visible = false;
            btnBack.Visible = false;

            btnConfirm.Enabled = false;
            btnUploadStudy.Enabled = false;
            btnEditClient.Enabled = false;
            btnDisplay.Enabled = false;
        }

        private void SetupStudiesGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№", Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate", HeaderText = "Дата", DataPropertyName = "StudyDate", Width = 150 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Название", DataPropertyName = "StudyName", Width = 250 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStudyId", HeaderText = "StudyId", DataPropertyName = "StudyId", Visible = false });

            txtSearch.Visible = false;
            btnAddClient.Visible = false;
            btnEditClient.Visible = false;
            btnUploadStudy.Visible = false;
            btnConfirm.Visible = false;

            txtSelectedName.Visible = true;
            txtSelectedPolicy.Visible = true;
            btnBack.Visible = true;
            btnDisplay.Visible = true;
            btnDisplay.Text = "Серии";

            btnDisplay.Click -= OnSeriesDisplayClicked;
            btnDisplay.Click -= OnSeriesShowClicked;
            btnDisplay.Click += OnSeriesDisplayClicked;

            btnConfirm.Enabled = false;
            btnUploadStudy.Enabled = false;
            btnDisplay.Enabled = false;
        }

        private void SetupSeriesGrid()
        {
            dgvClients.Columns.Clear();
            dgvClients.Rows.Clear();

            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNumber", HeaderText = "№", Width = 40 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle", HeaderText = "Название серии", DataPropertyName = "Title", Width = 400 });
            dgvClients.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSeriesId", HeaderText = "SeriesId", DataPropertyName = "SeriesId", Visible = false });

            txtSearch.Visible = false;
            btnAddClient.Visible = false;
            btnEditClient.Visible = false;
            btnUploadStudy.Visible = false;
            btnConfirm.Visible = false;

            txtSelectedName.Visible = true;
            txtSelectedPolicy.Visible = true;
            btnBack.Visible = true;
            btnDisplay.Visible = true;
            btnDisplay.Text = "Отобразить";

            btnDisplay.Click -= OnSeriesDisplayClicked;
            btnDisplay.Click -= OnSeriesShowClicked;
            btnDisplay.Click += OnSeriesShowClicked;

            btnDisplay.Enabled = false;
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

            var list = await _httpClient.GetFromJsonAsync<List<ClientDto>>(url)
                       ?? new List<ClientDto>();

            dgvClients.Rows.Clear();
            int i = 1;
            foreach (var c in list)
                dgvClients.Rows.Add(i++, c.FullName, c.MedPolicy, c.Id);

            UpdateButtonsState();
        }

        private async Task OnConfirmAsync()
        {
            if (dgvClients.SelectedRows.Count == 0) return;

            var row = dgvClients.SelectedRows[0];
            _selectedClientId = Convert.ToInt32(row.Cells["colId"].Value);
            txtSelectedName.Text = row.Cells["colName"].Value?.ToString() ?? "";
            txtSelectedPolicy.Text = row.Cells["colPolicy"].Value?.ToString() ?? "";

            SetupStudiesGrid();
            await LoadStudiesAsync(_selectedClientId);
        }

        private async Task OnEditClientAsync()
        {
            if (dgvClients.SelectedRows.Count == 0) return;

            var row = dgvClients.SelectedRows[0];
            int clientId = Convert.ToInt32(row.Cells["colId"].Value);
            using var dlg = new EditClientForm(_httpClient, clientId);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                await SearchClientsAsync(txtSearch.Text.Trim());
            }
        }

        private async Task LoadStudiesAsync(int clientId)
        {
            var list = await _httpClient
                .GetFromJsonAsync<List<StudyDto>>($"api/clients/{clientId}/studies")
                ?? new List<StudyDto>();

            dgvClients.Rows.Clear();
            int i = 1;
            foreach (var s in list)
                dgvClients.Rows.Add(i++, s.StudyDate.ToString("yyyy-MM-dd"), s.StudyName, s.StudyId);

            UpdateButtonsState();
        }

        private async Task OnUploadStudyAsync()
        {
            if (dgvClients.SelectedRows.Count == 0)
                return;

            var row = dgvClients.SelectedRows[0];
            int clientId = Convert.ToInt32(row.Cells["colId"].Value);

            using var dlg = new OpenFileDialog
            {
                Filter = "ZIP archives (*.zip)|*.zip|All files (*.*)|*.*",
                Title = "Выберите ZIP с DICOMDIR"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            byte[] bytes = await File.ReadAllBytesAsync(dlg.FileName);
            using var content = new MultipartFormDataContent();
            var zipContent = new ByteArrayContent(bytes);
            zipContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(zipContent, "archive", Path.GetFileName(dlg.FileName));

            var resp = await _httpClient
                .PostAsync($"api/clients/{clientId}/studies", content);
            resp.EnsureSuccessStatusCode();

            _selectedClientId = clientId;
            await LoadStudiesAsync(_selectedClientId);
        }

        private async void OnSeriesDisplayClicked(object? sender, EventArgs e)
        {
            if (dgvClients.SelectedRows.Count == 0) return;

            _selectedStudyId = dgvClients.SelectedRows[0]
                .Cells["colStudyId"].Value!.ToString()!;

            SetupSeriesGrid();
            await LoadSeriesAsync(_selectedStudyId);
        }

        private async Task LoadSeriesAsync(string studyId)
        {
            var list = await _httpClient
                .GetFromJsonAsync<List<SeriesDto>>($"api/studies/{Uri.EscapeDataString(studyId)}/series")
                ?? new List<SeriesDto>();

            dgvClients.Rows.Clear();
            int i = 1;
            foreach (var s in list)
                dgvClients.Rows.Add(i++, s.Title, s.SeriesId);

            UpdateButtonsState();
        }

        private void OnSeriesShowClicked(object? sender, EventArgs e)
        {
            if (dgvClients.SelectedRows.Count == 0) return;

            string seriesId = dgvClients.SelectedRows[0]
                .Cells["colSeriesId"].Value!.ToString()!;

            string seriesName = dgvClients.SelectedRows[0]
                .Cells["colTitle"].Value!.ToString()!;

            var viewer = new SeriesViewerForm(_httpClient, _selectedStudyId, seriesId, seriesName);
            viewer.Show(this);
        }

        private void OnBack()
        {
            if (btnDisplay.Text == "Отобразить")
            {
                SetupStudiesGrid();
                _ = LoadStudiesAsync(_selectedClientId);
            }
            else
            {
                SetupSearchGrid();
                dgvClients.Rows.Clear();
                txtSearch.Text = "";
                btnConfirm.Enabled = false;
                btnUploadStudy.Enabled = false;
                btnEditClient.Enabled = false;
                btnDisplay.Enabled = false;
            }
        }
    }
}
