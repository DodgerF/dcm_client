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

        private PictureBox picBox   = null!;
        private TrackBar  trackBar  = null!;
        private int       _currentIndex = -1;

        public SeriesViewerForm(HttpClient httpClient, string studyId, string seriesId, string seriesName)
        {
            _httpClient = httpClient;
            _studyId    = studyId;
            _seriesId   = seriesId;

            Text = $"Просмотр серии {seriesName}";
            ClientSize = new Size(800, 600);

            picBox = new PictureBox {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            trackBar = new TrackBar {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                TickStyle = TickStyle.None,
                Enabled = false
            };
            trackBar.Scroll += TrackBar_Scroll;

            Controls.Add(picBox);
            Controls.Add(trackBar);

            _ = LoadInstanceListAsync();
        }

        private async Task LoadInstanceListAsync()
        {
            try
            {
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

                trackBar.Maximum = _instanceIds.Count - 1;
                trackBar.Value   = 0;
                trackBar.Enabled = true;

                await LoadImageAtIndexAsync(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка изображений:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private async void TrackBar_Scroll(object sender, EventArgs e)
        {
            int idx = trackBar.Value;
            if (idx != _currentIndex)
            {
                await LoadImageAtIndexAsync(idx);
            }
        }

        private async Task LoadImageAtIndexAsync(int idx)
        {
            try
            {
                _currentIndex = idx;
                string instId = _instanceIds[idx];

                string url = $"api/studies/{Uri.EscapeDataString(_studyId)}/series/"
                           + $"{Uri.EscapeDataString(_seriesId)}/instances/"
                           + $"{Uri.EscapeDataString(instId)}/preview";

                using var stream = await _httpClient.GetStreamAsync(url);
                var img = Image.FromStream(stream);

                picBox.Image?.Dispose();
                picBox.Image = img;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить картинку:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
