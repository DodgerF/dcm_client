using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public class SeriesViewerForm : Form
    {
        private readonly HttpClient _httpClient;
        private readonly string     _studyId;
        private readonly string     _seriesId;

        private PictureBox picBox = null!;
        private TrackBar   trackBar = null!;
        private TextBox    txtFrameIndex = null!;
        private Label      lblTotalFrames = null!;

        // Label для отображения координат внутри picBox
        private Label      lblMouseCoords = null!;

        private ComboBox   cmbPresets = null!;
        private Label      lblWW = null!;
        private TextBox    txtWindowWidth = null!;
        private Label      lblSlash = null!;
        private Label      lblWL = null!;
        private TextBox    txtWindowLevel = null!;

        // Кнопки HU, R, S
        private Button     btnHU = null!;
        private Button     btnR  = null!;
        private Button     btnS  = null!;

        // TextBox для вывода результата (HU или расстояние)
        private TextBox    txtResult = null!;

        // Для зума/панорамы: оригинальное и текущее масштабированное изображение
        private Bitmap?    _originalImage;
        private Bitmap?    _currentScaledImage;
        private float      _zoomFactor = 1.0f;
        private const float ZoomStep = 0.1f;
        private const float MinZoom = 0.1f;
        private const float MaxZoom = 10.0f;

        // Смещение изображения внутри picBox для панорамирования
        private int _offsetX = 0;
        private int _offsetY = 0;

        // Флаги и данные для перетаскивания (панорама) на ПКМ
        private bool _isPanning = false;
        private int  _panStartMouseX = 0;
        private int  _panStartMouseY = 0;
        private int  _panStartOffsetX = 0;
        private int  _panStartOffsetY = 0;

        // Предустановки WW/WL
        private readonly Dictionary<string, (int WW, int WL)> _windowPresets =
            new Dictionary<string, (int, int)>
            {
                { "Мягкие ткани",      (350,   40)   },
                { "Костные ткани",     (2000,  400)  },
                { "Лёгкие",            (1500, -600)  },
                { "Пользовательские",  (0,     0)    }
            };

        // Список instanceId-ов текущей серии
        private List<string> _instanceIds = new List<string>();

        // HU-режим
        private bool _huMode = false;

        // Данные маркера HU (пиксели оригинала + instanceId)
        private int?   _huImgX = null;
        private int?   _huImgY = null;
        private string? _huInstId = null;

        // R-режим (линейка)
        private bool _rMode = false;
        private bool _rAwaitingSecond = false;
        private string? _rInstId1 = null;
        private int?    _rImgX1 = null;
        private int?    _rImgY1 = null;
        private string? _rInstId2 = null;
        private int?    _rImgX2 = null;
        private int?    _rImgY2 = null;

        public SeriesViewerForm(HttpClient httpClient, string studyId, string seriesId, string seriesName)
        {
            _httpClient = httpClient;
            _studyId    = studyId;
            _seriesId   = seriesId;

            Text = $"Просмотр серии: {seriesName}";
            ClientSize = new Size(820, 780);
            MinimumSize = new Size(820, 780);

            InitializeControls();
            AttachEvents();

            _ = LoadInstanceListAsync();
        }

        private void InitializeControls()
        {
            //
            // 1. PictureBox: фиксированный размер 780×520
            //
            picBox = new PictureBox
            {
                Location    = new Point(10, 10),
                Size        = new Size(780, 520),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.Black,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left
            };
            picBox.MouseWheel += PicBox_MouseWheel;
            picBox.Paint      += PicBox_Paint;
            picBox.MouseClick += PicBox_MouseClick;
            picBox.MouseDown  += PicBox_MouseDown;
            picBox.MouseMove  += PicBox_MouseMove;
            picBox.MouseUp    += PicBox_MouseUp;

            // Label для координат (дочерний элемент picBox)
            lblMouseCoords = new Label
            {
                AutoSize  = true,
                BackColor = Color.FromArgb(160, Color.Black),
                ForeColor = Color.White,
                Text      = "(X: -, Y: -)",
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right
            };
            lblMouseCoords.Location = new Point(
                picBox.ClientSize.Width - lblMouseCoords.Width - 15,
                picBox.ClientSize.Height - lblMouseCoords.Height - 5
            );
            picBox.Controls.Add(lblMouseCoords);

            Controls.Add(picBox);

            //
            // 2. TrackBar под PictureBox
            //
            trackBar = new TrackBar
            {
                Location   = new Point(10, picBox.Bottom + 10),
                Size       = new Size(780, 45),
                Minimum    = 0,
                Maximum    = 0,
                TickStyle  = TickStyle.None,
                Enabled    = false,
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(trackBar);

            //
            // 3. TextBox для номера кадра
            //
            txtFrameIndex = new TextBox
            {
                Location  = new Point(10, trackBar.Bottom + 10),
                Size      = new Size(50, 20),
                TextAlign = HorizontalAlignment.Right,
                Enabled   = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(txtFrameIndex);

            //
            // 4. Label «/ total»
            //
            lblTotalFrames = new Label
            {
                Location  = new Point(65, trackBar.Bottom + 13),
                AutoSize  = true,
                Text      = "/ 0",
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblTotalFrames);

            //
            // --- Блок предустановок + WW/WL под номером кадра ---
            //
            cmbPresets = new ComboBox
            {
                Location      = new Point(10, txtFrameIndex.Bottom + 15),
                Size          = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor        = AnchorStyles.Top | AnchorStyles.Left
            };
            foreach (var key in _windowPresets.Keys)
            {
                cmbPresets.Items.Add(key);
            }
            cmbPresets.SelectedItem = "Мягкие ткани";
            Controls.Add(cmbPresets);

            lblWW = new Label
            {
                Location = new Point(170, txtFrameIndex.Bottom + 20),
                AutoSize = true,
                Text     = "Window Width:",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblWW);

            txtWindowWidth = new TextBox
            {
                Location = new Point(lblWW.Right + 5, txtFrameIndex.Bottom + 15),
                Size     = new Size(60, 25),
                ReadOnly = true,
                Text     = _windowPresets["Мягкие ткани"].WW.ToString(),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(txtWindowWidth);

            lblSlash = new Label
            {
                Location = new Point(txtWindowWidth.Right + 5, txtFrameIndex.Bottom + 20),
                AutoSize = true,
                Text     = "/",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblSlash);

            lblWL = new Label
            {
                Location = new Point(lblSlash.Right + 10, txtFrameIndex.Bottom + 20),
                AutoSize = true,
                Text     = "Window Level:",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(lblWL);

            txtWindowLevel = new TextBox
            {
                Location = new Point(lblWL.Right + 5, txtFrameIndex.Bottom + 15),
                Size     = new Size(60, 25),
                ReadOnly = true,
                Text     = _windowPresets["Мягкие ткани"].WL.ToString(),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(txtWindowLevel);

            //
            // --- Кнопки HU, R, S ---
            //
            int buttonsY = txtWindowLevel.Bottom + 20;
            btnHU = new Button
            {
                Location = new Point(10, buttonsY),
                Size     = new Size(60, 30),
                Text     = "HU",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(btnHU);

            btnR = new Button
            {
                Location = new Point(btnHU.Right + 10, buttonsY),
                Size     = new Size(60, 30),
                Text     = "R",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(btnR);

            btnS = new Button
            {
                Location = new Point(btnR.Right + 10, buttonsY),
                Size     = new Size(60, 30),
                Text     = "S",
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(btnS);

            //
            //  . TextBox для результата (HU или расстояние)
            //
            txtResult = new TextBox
            {
                Location  = new Point(10, btnHU.Bottom + 15),
                Size      = new Size(400, 25),
                ReadOnly  = true,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(txtResult);
        }

        private void AttachEvents()
        {
            //
            // TrackBar: загрузка кадра при прокрутке
            //
            trackBar.Scroll += async (_, __) =>
            {
                int idx = trackBar.Value;
                txtFrameIndex.Text = (idx + 1).ToString();
                await LoadAndDisplayInstanceAsync(idx);
            };

            //
            // Enter в txtFrameIndex
            //
            txtFrameIndex.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled          = true;
                    e.SuppressKeyPress = true;

                    if (int.TryParse(txtFrameIndex.Text.Trim(), out int userIdx))
                    {
                        int zeroBased = userIdx - 1;
                        if (zeroBased >= 0 && zeroBased < _instanceIds.Count)
                        {
                            trackBar.Value = zeroBased;
                            await LoadAndDisplayInstanceAsync(zeroBased);
                        }
                        else
                        {
                            MessageBox.Show("Неверный номер кадра.", "Ошибка",
                                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            };

            //
            // MouseMove внутри picBox: обновляем lblMouseCoords
            //
            picBox.MouseMove += (sender, e) =>
            {
                if (_currentScaledImage == null || _originalImage == null)
                {
                    lblMouseCoords.Text = "(X: -, Y: -)";
                    return;
                }

                int sW = _currentScaledImage.Width;
                int sH = _currentScaledImage.Height;

                // Координаты курсора относительно области изображения
                int relX = e.X - _offsetX;
                int relY = e.Y - _offsetY;

                if (relX >= 0 && relX < sW && relY >= 0 && relY < sH)
                {
                    float imgXf = relX / _zoomFactor;
                    float imgYf = relY / _zoomFactor;

                    int ix = Math.Min(Math.Max((int)imgXf, 0), _originalImage.Width - 1);
                    int iy = Math.Min(Math.Max((int)imgYf, 0), _originalImage.Height - 1);

                    lblMouseCoords.Text = $"(X: {ix}, Y: {iy})";
                }
                else
                {
                    lblMouseCoords.Text = "(X: -, Y: -)";
                }

                // Если мы в режиме пана (ПКМ зажата), панорамируем
                if (_isPanning && _currentScaledImage != null)
                {
                    int dx = e.X - _panStartMouseX;
                    int dy = e.Y - _panStartMouseY;
                    _offsetX = _panStartOffsetX + dx;
                    _offsetY = _panStartOffsetY + dy;

                    // Проверим границы по X
                    int imgW = _currentScaledImage.Width;
                    int boxW = picBox.ClientSize.Width;
                    if (imgW > boxW)
                    {
                        int minX = boxW - imgW;
                        int maxX = 0;
                        _offsetX = Math.Min(Math.Max(_offsetX, minX), maxX);
                    }
                    else
                    {
                        _offsetX = (boxW - imgW) / 2;
                    }

                    // Проверим границы по Y
                    int imgH = _currentScaledImage.Height;
                    int boxH = picBox.ClientSize.Height;
                    if (imgH > boxH)
                    {
                        int minY = boxH - imgH;
                        int maxY = 0;
                        _offsetY = Math.Min(Math.Max(_offsetY, minY), maxY);
                    }
                    else
                    {
                        _offsetY = (boxH - imgH) / 2;
                    }

                    picBox.Invalidate();
                }
            };

            //
            // MouseWheel для зума
            //
            picBox.MouseWheel += PicBox_MouseWheel;

            //
            // Paint для отрисовки изображения, маркеров и координат
            //
            picBox.Paint += PicBox_Paint;

            //
            // MouseDown/MouseUp для панорамы (ПКМ)
            //
            picBox.MouseDown += PicBox_MouseDown;
            picBox.MouseUp   += PicBox_MouseUp;

            //
            // Resize picBox: пересчитываем центр/панораму картинки
            //
            picBox.Resize += (sender, e) =>
            {
                CenterImageInView();
                picBox.Invalidate();
            };

            //
            // Смена пресетов WW/WL
            //
            cmbPresets.SelectedIndexChanged += (sender, e) =>
            {
                if (cmbPresets.SelectedItem is not string selName) return;
                if (!_windowPresets.ContainsKey(selName)) return;

                if (selName != "Пользовательские")
                {
                    var (ww, wl) = _windowPresets[selName];
                    txtWindowWidth.Text = ww.ToString();
                    txtWindowLevel.Text = wl.ToString();
                    txtWindowWidth.ReadOnly = true;
                    txtWindowLevel.ReadOnly = true;
                }
                else
                {
                    txtWindowWidth.ReadOnly = false;
                    txtWindowLevel.ReadOnly = false;
                }
            };

            //
            // Ввод в WW/WL вручную → переключаемся на «Пользовательские»
            //
            txtWindowWidth.TextChanged += (sender, e) => OnWindowTextChanged();
            txtWindowLevel.TextChanged += (sender, e) => OnWindowTextChanged();

            //
            // Кнопка HU: включаем HU-режим
            //
            btnHU.Click += (sender, e) =>
            {
                _huMode = true;
                _rMode = false;
                btnHU.BackColor = Color.LightBlue;
                btnR.BackColor  = SystemColors.Control;
                btnS.BackColor  = SystemColors.Control;
                ClearAllMarkers();
                txtResult.Text = "";
            };

            //
            // Кнопка R: включаем R-режим (линейка)
            //
            btnR.Click += (sender, e) =>
            {
                _rMode = true;
                _huMode = false;
                _rAwaitingSecond = false;
                btnR.BackColor  = Color.LightBlue;
                btnHU.BackColor = SystemColors.Control;
                btnS.BackColor  = SystemColors.Control;
                ClearAllMarkers();
                txtResult.Text = "";
            };

            //
            // Кнопка S: сбрасываем оба режима
            //
            btnS.Click += (sender, e) =>
            {
                _rMode = false;
                _huMode = false;
                btnS.BackColor  = Color.LightBlue;
                btnHU.BackColor = SystemColors.Control;
                btnR.BackColor  = SystemColors.Control;
                ClearAllMarkers();
                txtResult.Text = "";
            };

            //
            // При изменении размера формы (Anchor само двигает контролы)
            //
            this.Resize += (sender, e) =>
            {
                CenterImageInView();
                picBox.Invalidate();
            };
        }

        private void OnWindowTextChanged()
        {
            bool parsedWw = int.TryParse(txtWindowWidth.Text.Trim(), out int currentWw);
            bool parsedWl = int.TryParse(txtWindowLevel.Text.Trim(), out int currentWl);

            if (!parsedWw || !parsedWl)
            {
                SetPresetSilently("Пользовательские");
                txtWindowWidth.ReadOnly = false;
                txtWindowLevel.ReadOnly = false;
                return;
            }

            foreach (var kvp in _windowPresets)
            {
                string presetName = kvp.Key;
                var (presetWw, presetWl) = kvp.Value;
                if (presetName == "Пользовательские") continue;

                if (currentWw == presetWw && currentWl == presetWl)
                {
                    SetPresetSilently(presetName);
                    txtWindowWidth.ReadOnly = true;
                    txtWindowLevel.ReadOnly = true;
                    return;
                }
            }

            SetPresetSilently("Пользовательские");
            txtWindowWidth.ReadOnly = false;
            txtWindowLevel.ReadOnly = false;
        }

        private void SetPresetSilently(string presetName)
        {
            if (cmbPresets.SelectedItem as string == presetName) return;
            cmbPresets.SelectedIndexChanged -= CmbPresets_SelectedIndexChanged;
            cmbPresets.SelectedItem = presetName;
            cmbPresets.SelectedIndexChanged += CmbPresets_SelectedIndexChanged;
        }

        private void CmbPresets_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbPresets.SelectedItem is not string selName) return;
            if (!_windowPresets.ContainsKey(selName)) return;

            if (selName != "Пользовательские")
            {
                var (ww, wl) = _windowPresets[selName];
                txtWindowWidth.Text = ww.ToString();
                txtWindowLevel.Text = wl.ToString();
                txtWindowWidth.ReadOnly = true;
                txtWindowLevel.ReadOnly = true;
            }
            else
            {
                txtWindowWidth.ReadOnly = false;
                txtWindowLevel.ReadOnly = false;
            }
        }

        private async Task LoadInstanceListAsync()
        {
            try
            {
                _instanceIds = await _httpClient
                    .GetFromJsonAsync<List<string>>(
                        $"api/studies/{_studyId}/series/{_seriesId}/instances"
                    ) ?? new List<string>();

                if (_instanceIds.Count > 0)
                {
                    trackBar.Maximum = _instanceIds.Count - 1;
                    trackBar.Enabled = true;
                    txtFrameIndex.Enabled = true;
                    lblTotalFrames.Text  = $"/ {_instanceIds.Count}";

                    trackBar.Value = 0;
                    txtFrameIndex.Text = "1";
                    await LoadAndDisplayInstanceAsync(0);
                }
                else
                {
                    MessageBox.Show("В серии не найдено ни одного инстанса.",
                                    "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка инстансов:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadAndDisplayInstanceAsync(int index)
        {
            if (index < 0 || index >= _instanceIds.Count) return;
            string instId = _instanceIds[index];

            try
            {
                string url = $"api/studies/{_studyId}/series/{_seriesId}/instances/{instId}/preview";
                bool hasWw = int.TryParse(txtWindowWidth.Text.Trim(), out int ww);
                bool hasWl = int.TryParse(txtWindowLevel.Text.Trim(), out int wl);
                if (hasWw && hasWl)
                    url += $"?ww={ww}&wl={wl}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                byte[] pngBytes = await response.Content.ReadAsByteArrayAsync();
                using var ms = new System.IO.MemoryStream(pngBytes);
                var loaded = new Bitmap(ms);

                _originalImage = new Bitmap(loaded);
                _zoomFactor    = 1.0f;
                RecalculateCurrentScaledImage();

                CenterImageInView();
                picBox.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить превью кадра:\n{ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Пересоздаёт _currentScaledImage на основе _originalImage и _zoomFactor.
        /// </summary>
        private void RecalculateCurrentScaledImage()
        {
            if (_originalImage == null) return;

            _currentScaledImage?.Dispose();
            int newW = (int)(_originalImage.Width * _zoomFactor);
            int newH = (int)(_originalImage.Height * _zoomFactor);
            _currentScaledImage = new Bitmap(_originalImage, new Size(newW, newH));
            picBox.Invalidate();
        }

        /// <summary>
        /// Центрирует изображение, если оно меньше picBox; иначе оставляет текущее смещение.
        /// </summary>
        private void CenterImageInView()
        {
            if (_currentScaledImage == null) return;

            int imgW = _currentScaledImage.Width;
            int imgH = _currentScaledImage.Height;
            int boxW = picBox.ClientSize.Width;
            int boxH = picBox.ClientSize.Height;

            if (imgW < boxW)
                _offsetX = (boxW - imgW) / 2;
            if (imgH < boxH)
                _offsetY = (boxH - imgH) / 2;
        }

        /// <summary>
        /// Рисует изображение, маркеры HU / R, и обновляет координаты.
        /// </summary>
        private void PicBox_Paint(object? sender, PaintEventArgs e)
        {
            if (_currentScaledImage == null || _originalImage == null)
                return;

            // 1) Рисуем масштабированное изображение с учётом смещений
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_currentScaledImage, _offsetX, _offsetY, _currentScaledImage.Width, _currentScaledImage.Height);

            // 2) Маркер HU, если он установлен, и совпадает instanceId
            if (_huImgX != null && _huImgY != null && _huInstId != null)
            {
                string currentInst = _instanceIds[trackBar.Value];
                if (_huInstId == currentInst)
                {
                    int imgX = _huImgX.Value;
                    int imgY = _huImgY.Value;
                    float screenXf = _offsetX + imgX * _zoomFactor;
                    float screenYf = _offsetY + imgY * _zoomFactor;
                    int size = 10, half = size / 2;
                    using var brush = new SolidBrush(Color.Red);
                    e.Graphics.FillRectangle(brush, screenXf - half, screenYf - half, size, size);
                }
            }

            // 3) Маркеры R: две точки, если заданы, и совпадают instanceId
            if (_rImgX1 != null && _rImgY1 != null && _rInstId1 != null)
            {
                string currentInst = _instanceIds[trackBar.Value];
                if (_rInstId1 == currentInst)
                {
                    float screenXf = _offsetX + _rImgX1.Value * _zoomFactor;
                    float screenYf = _offsetY + _rImgY1.Value * _zoomFactor;
                    int size = 10, half = size / 2;
                    using var brush = new SolidBrush(Color.Green);
                    e.Graphics.FillRectangle(brush, screenXf - half, screenYf - half, size, size);
                }
            }
            if (_rImgX2 != null && _rImgY2 != null && _rInstId2 != null)
            {
                string currentInst = _instanceIds[trackBar.Value];
                if (_rInstId2 == currentInst)
                {
                    float screenXf = _offsetX + _rImgX2.Value * _zoomFactor;
                    float screenYf = _offsetY + _rImgY2.Value * _zoomFactor;
                    int size = 10, half = size / 2;
                    using var brush = new SolidBrush(Color.Green);
                    e.Graphics.FillRectangle(brush, screenXf - half, screenYf - half, size, size);
                }
            }
        }

        /// <summary>
        /// Обработчик колесика мыши: зумируем картинку, удерживая курсор на том же месте.
        /// </summary>
        private void PicBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (_originalImage == null) return;

            // Запомним координаты курсора в координатах оригинала
            int oldImgX = (int)((e.X - _offsetX) / _zoomFactor);
            int oldImgY = (int)((e.Y - _offsetY) / _zoomFactor);

            if (e.Delta > 0)
                _zoomFactor = Math.Min(_zoomFactor + ZoomStep, MaxZoom);
            else
                _zoomFactor = Math.Max(_zoomFactor - ZoomStep, MinZoom);

            RecalculateCurrentScaledImage();

            // Пересчитаем смещение, чтобы курсор оставался над тем же пикселем
            int newScreenX = (int)(oldImgX * _zoomFactor);
            int newScreenY = (int)(oldImgY * _zoomFactor);
            _offsetX = e.X - newScreenX;
            _offsetY = e.Y - newScreenY;

            CenterImageInView();
            picBox.Invalidate();
        }

        /// <summary>
        /// При нажатии кнопки мыши: ПКМ начинает панораму, ЛКМ – обработка HU или R.
        /// </summary>
        private void PicBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Начинаем панораму
                _isPanning = true;
                _panStartMouseX = e.X;
                _panStartMouseY = e.Y;
                _panStartOffsetX = _offsetX;
                _panStartOffsetY = _offsetY;
                picBox.Cursor = Cursors.Hand;
            }
        }

        private void PicBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _isPanning)
            {
                _isPanning = false;
                picBox.Cursor = Cursors.Default;
            }
        }

        private void PicBox_MouseMove(object? sender, MouseEventArgs e)
        {
            // Логика обновления координат и панорамирования реализована в MouseMove через AttachEvents
        }

        private async void PicBox_MouseClick(object? sender, MouseEventArgs e)
        {
            // Определяем, в каком режиме мы находимся: HU или R
            if (_huMode)
            {
                await HandleHUClickAsync(e);
            }
            else if (_rMode)
            {
                await HandleRClickAsync(e);
            }
        }

        /// <summary>
        /// Обрабатывает клик в HU-режиме: вычисляем координаты в изображении,
        /// запрашиваем плотность и выводим результат.
        /// </summary>
        private async Task HandleHUClickAsync(MouseEventArgs e)
        {
            if (_currentScaledImage == null || _originalImage == null)
                return;

            // 1) Переводим координаты клика в пиксели оригинала
            if (!TryGetImageCoordinates(e.X, e.Y, out int imgX, out int imgY))
                return;

            // Сохраняем маркер HU и выключаем режим
            string currentInst = _instanceIds[trackBar.Value];
            _huImgX   = imgX;
            _huImgY   = imgY;
            _huInstId = currentInst;
            _huMode   = false;
            btnHU.BackColor = SystemColors.Control;
            picBox.Invalidate();

            // 2) Запрашиваем значение HU у сервера
            try
            {
                string reqUrl = $"api/instances/{currentInst}/density?x={imgX}&y={imgY}";
                var response = await _httpClient.GetAsync(reqUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Сервер вернул ошибку при получении HU:\n{error}",
                        "Ошибка HU",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    txtResult.Text = "";
                    return;
                }

                // Читаем JSON вида {"hu":353}
                string content = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("hu", out var huElement) &&
                    huElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    int huValue = huElement.GetInt32();
                    txtResult.Text = $"Плотность ткани в точке равна {huValue} HU";
                }
                else
                {
                    txtResult.Text = $"Неверный формат ответа: {content}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при запросе HU на сервер:\n{ex.Message}",
                    "Ошибка HU",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                txtResult.Text = "";
            }
        }


        /// <summary>
        /// Обрабатывает клик в R-режиме (линейка): последовательно запоминает две точки,
        /// после второй — запрашивает у сервера расстояние и выводит округлённое до 3 знаков значение.
        /// </summary>
        private async Task HandleRClickAsync(MouseEventArgs e)
        {
            if (_currentScaledImage == null || _originalImage == null)
                return;

            // Конвертируем экранные координаты в пиксели оригинального изображения
            if (!TryGetImageCoordinates(e.X, e.Y, out int imgX, out int imgY))
                return;

            string currentInst = _instanceIds[trackBar.Value];

            if (!_rAwaitingSecond)
            {
                // Первая точка
                _rInstId1        = currentInst;
                _rImgX1          = imgX;
                _rImgY1          = imgY;
                _rAwaitingSecond = true;
                picBox.Invalidate();
            }
            else
            {
                // Вторая точка
                _rInstId2        = currentInst;
                _rImgX2          = imgX;
                _rImgY2          = imgY;
                _rAwaitingSecond = false;
                _rMode           = false;
                btnR.BackColor   = SystemColors.Control;
                picBox.Invalidate();

                // Теперь отправляем запрос на сервер
                if (_rInstId1 != null && _rImgX1.HasValue && _rImgY1.HasValue
                    && _rInstId2 != null && _rImgX2.HasValue && _rImgY2.HasValue)
                {
                    try
                    {
                        string url = $"api/distance" +
                                    $"?instanceId1={_rInstId1}" +
                                    $"&x1={_rImgX1.Value}&y1={_rImgY1.Value}" +
                                    $"&instanceId2={_rInstId2}" +
                                    $"&x2={_rImgX2.Value}&y2={_rImgY2.Value}";

                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode)
                        {
                            string error = await response.Content.ReadAsStringAsync();
                            MessageBox.Show(
                                $"Сервер вернул ошибку при расчёте расстояния:\n{error}",
                                "Ошибка Distance",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            txtResult.Text = "";
                            return;
                        }

                        // Разбираем JSON-ответ и округляем значение до 3 знаков
                        using var stream = await response.Content.ReadAsStreamAsync();
                        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
                        if (doc.RootElement.TryGetProperty("distance", out var element) &&
                            element.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            double distanceMm = element.GetDouble();
                            // Округляем до 3 знаков после запятой
                            distanceMm = Math.Round(distanceMm, 3);
                            txtResult.Text = $"Расстояние между двумя точками {distanceMm:F3} мм";
                        }
                        else
                        {
                            txtResult.Text = "Ответ сервера не содержит поле \"distance\".";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Ошибка при запросе расстояния на сервер:\n{ex.Message}",
                            "Ошибка Distance",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        txtResult.Text = "";
                    }
                }
            }
        }

        /// <summary>
        /// Преобразует координаты мыши (screenX, screenY) в координаты внутри оригинального изображения.
        /// Возвращает true, если клик попал в область картинки, и тогда imgX/imgY принимают реальные значения.
        /// </summary>
        private bool TryGetImageCoordinates(int screenX, int screenY, out int imgX, out int imgY)
        {
            imgX = imgY = 0;
            if (_currentScaledImage == null || _originalImage == null)
                return false;

            int imgW = _currentScaledImage.Width;
            int imgH = _currentScaledImage.Height;

            int relX = screenX - _offsetX;
            int relY = screenY - _offsetY;
            if (relX < 0 || relX >= imgW || relY < 0 || relY >= imgH)
                return false;

            float xf = relX / _zoomFactor;
            float yf = relY / _zoomFactor;
            imgX = Math.Min(Math.Max((int)xf, 0), _originalImage.Width - 1);
            imgY = Math.Min(Math.Max((int)yf, 0), _originalImage.Height - 1);
            return true;
        }


        private void ClearAllMarkers()
        {
            _huImgX = null;
            _huImgY = null;
            _huInstId = null;

            _rInstId1 = null;
            _rImgX1 = null;
            _rImgY1 = null;

            _rInstId2 = null;
            _rImgX2 = null;
            _rImgY2 = null;

            picBox.Invalidate();
        }

    }
}
