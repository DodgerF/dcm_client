using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private List<string>        _instanceIds = new();

        private PictureBox picBox    = null!;
        private TrackBar  trackBar   = null!;
        private TextBox   txtIndex   = null!;
        private Label     lblCount   = null!;
        private Label     lblCoords  = null!;

        private int       _currentIndex = -1;

        public SeriesViewerForm(HttpClient httpClient, string studyId, string seriesId, string seriesName)
        {
            _httpClient = httpClient;
            _studyId    = studyId;
            _seriesId   = seriesId;

            // Сначала создаём все контролы, чтобы OnResize не «падал» на null
            InitializeControls();

            // Теперь безопасно задаём размер и заголовок
            Text = $"Просмотр серии: {seriesName}";
            ClientSize = new Size(800, 650);
            MinimumSize = new Size(600, 500);

            // Начинаем асинхронную загрузку списка инстансов
            _ = LoadInstanceListAsync();
        }

        private void InitializeControls()
        {
            // PictureBox, занимающий всю «верхнюю» область
            picBox = new PictureBox
            {
                Location = new Point(0, 0),
                Size = new Size(800, 550), // временные размеры; в OnResize подправим
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            // Подписываемся на MouseMove для вычисления координат
            picBox.MouseMove += PicBox_MouseMove;

            // TrackBar внизу
            trackBar = new TrackBar
            {
                Location = new Point(10, 560), // временные значения
                Size = new Size(780, 45),      // временные значения
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Minimum = 0,
                TickStyle = TickStyle.None,
                Enabled = false
            };
            trackBar.Scroll += TrackBar_Scroll;

            // TextBox для ввода номера снимка (1-based)
            txtIndex = new TextBox
            {
                Location = new Point(10, 615), // временные
                Size = new Size(50, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            txtIndex.KeyDown += TxtIndex_KeyDown;

            // Label «/ N»
            lblCount = new Label
            {
                Text = "/ 0",
                Location = new Point(65, 618), // временные
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // Label для координат мыши (пиксели по X,Y)
            lblCoords = new Label
            {
                Text = "(0, 0)",
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            Controls.Add(picBox);
            Controls.Add(trackBar);
            Controls.Add(txtIndex);
            Controls.Add(lblCount);
            Controls.Add(lblCoords);
        }

        private async Task LoadInstanceListAsync()
        {
            try
            {
                // Предполагаем, что сервер отдаёт JSON-массив всех InstanceId в серии
                string url = $"api/studies/{Uri.EscapeDataString(_studyId)}/series/"
                           + $"{Uri.EscapeDataString(_seriesId)}/instances";

                _instanceIds = await _httpClient
                    .GetFromJsonAsync<List<string>>(url)
                    ?? new List<string>();

                if (_instanceIds.Count == 0)
                {
                    MessageBox.Show("В серии нет изображений.", "Внимание",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                    return;
                }

                // Настраиваем TrackBar, txtIndex, lblCount
                trackBar.Maximum = _instanceIds.Count - 1;
                trackBar.Value = 0;
                trackBar.Enabled = true;

                lblCount.Text = $"/ {_instanceIds.Count}";
                txtIndex.Text = "1";

                // Сразу загружаем первое изображение:
                await LoadImageAtIndexAsync(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка изображений:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private async void TrackBar_Scroll(object? sender, EventArgs e)
        {
            int idx = trackBar.Value;
            if (idx != _currentIndex)
            {
                await LoadImageAtIndexAsync(idx);
            }
        }

        private async void TxtIndex_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                if (int.TryParse(txtIndex.Text.Trim(), out int inputNumber))
                {
                    if (inputNumber >= 1 && inputNumber <= _instanceIds.Count)
                    {
                        int newIndex = inputNumber - 1;
                        if (newIndex != _currentIndex)
                        {
                            trackBar.Value = newIndex;
                            await LoadImageAtIndexAsync(newIndex);
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Введите число от 1 до {_instanceIds.Count}.",
                            "Неверный ввод",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                        txtIndex.Text = (_currentIndex + 1).ToString();
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Введите корректный номер снимка (целое число).",
                        "Неверный ввод",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    txtIndex.Text = (_currentIndex + 1).ToString();
                }
            }
        }

        private async Task LoadImageAtIndexAsync(int idx)
        {
            try
            {
                _currentIndex = idx;
                string instId = _instanceIds[idx];

                // URL для получения превью картинки (PNG или JPEG).
                // Сервер должен поддерживать такой эндпоинт:
                // GET /api/studies/{studyId}/series/{seriesId}/instances/{instanceId}/preview
                string url = $"api/studies/{Uri.EscapeDataString(_studyId)}/series/"
                           + $"{Uri.EscapeDataString(_seriesId)}/instances/"
                           + $"{Uri.EscapeDataString(instId)}/preview";

                using var stream = await _httpClient.GetStreamAsync(url);
                var img = Image.FromStream(stream);

                picBox.Image?.Dispose();
                picBox.Image = img;

                txtIndex.Text = (idx + 1).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить картинку:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PicBox_MouseMove(object? sender, MouseEventArgs e)
        {
            // Если изображения нет или контрол не инициализирован — ничего не показываем
            if (picBox.Image == null)
            {
                lblCoords.Text = "(–, –)";
                return;
            }

            // Размер оригинальной картинки
            Size imgSize = picBox.Image.Size;
            // Размер области, в которой картинка фактически отображается (учитывая Zoom)
            Size ctrlSize = picBox.ClientSize;

            // Вычисляем коэффициент масштабирования (один и тот же по ширине и высоте, так как SizeMode=Zoom)
            float ratio = Math.Min(
                (float)ctrlSize.Width / imgSize.Width,
                (float)ctrlSize.Height / imgSize.Height
            );

            // Фактический размер, в котором изображение отрисовано
            int drawWidth  = (int)(imgSize.Width * ratio);
            int drawHeight = (int)(imgSize.Height * ratio);

            // Смещение внутри PictureBox (центруем картинку)
            int offsetX = (ctrlSize.Width  - drawWidth)  / 2;
            int offsetY = (ctrlSize.Height - drawHeight) / 2;

            // Координата курсора относительно области, где картинка отрисована
            int xInImageArea = e.X - offsetX;
            int yInImageArea = e.Y - offsetY;

            if (xInImageArea < 0 || yInImageArea < 0 ||
                xInImageArea >= drawWidth || yInImageArea >= drawHeight)
            {
                // Курсор «за пределами» изображения (в полях)
                lblCoords.Text = "(–, –)";
            }
            else
            {
                // Переводим в координаты оригинального изображения
                int imgX = (int)(xInImageArea / ratio);
                int imgY = (int)(yInImageArea / ratio);
                lblCoords.Text = $"({imgX}, {imgY})";
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Если контролы ещё не созданы — выходим
            if (picBox == null || trackBar == null || txtIndex == null || lblCount == null || lblCoords == null)
                return;

            // Минимальные допустимые размеры формы
            int minHeight = 250;
            int minWidth = 300;
            if (ClientSize.Height < minHeight || ClientSize.Width < minWidth)
                return;

            // Переконфигурируем размеры и позиции контролов
            picBox.Size = new Size(ClientSize.Width, ClientSize.Height - 100);

            trackBar.Location = new Point(10, ClientSize.Height - 85);
            trackBar.Size = new Size(ClientSize.Width - 20, 45);

            txtIndex.Location = new Point(10, ClientSize.Height - 35);
            // lblCount сразу справа от txtIndex
            lblCount.Location = new Point(txtIndex.Right + 5, ClientSize.Height - 32);

            // lblCoords — в нижнем правом углу
            lblCoords.Location = new Point(
                ClientSize.Width - lblCoords.PreferredWidth - 10,
                ClientSize.Height - lblCoords.PreferredHeight - 5
            );
        }
    }
}
