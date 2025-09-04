using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MovingServer
{
    public partial class Form1 : Form
    {
        private TcpListener? listener;
        private readonly List<TcpClient> clients = new List<TcpClient>();
        private readonly object clientsLock = new object();
        private System.Windows.Forms.Timer? drawTimer;
        private Rectangle square;
        private int dx = 6, dy = 4;
        private int port = 5000;

        public Form1()
        {
            InitializeComponent();
            this.Text = "Server - MovingObject";
            this.ClientSize = new Size(400, 360);
            this.DoubleBuffered = true;

            square = new Rectangle(50, 50, 32, 32);
            this.Paint += Form1_Paint;

            StartServer();
            StartTimer();
        }

        private void StartServer()
        {
            Task.Run(async () =>
            {
                try
                {
                    listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    Console.WriteLine($"Server listening on port {port}");

                    while (true)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        lock (clientsLock) clients.Add(client);
                        Console.WriteLine("Client connected: " + client.Client.RemoteEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Server error: " + ex.Message);
                }
            });
        }

        private void StartTimer()
        {
            drawTimer = new System.Windows.Forms.Timer();
            drawTimer.Interval = 40;
            drawTimer.Tick += DrawTimer_Tick;
            drawTimer.Start();
        }

        private async void DrawTimer_Tick(object? sender, EventArgs e)
        {
            var bounds = this.ClientRectangle;
            square.X += dx;
            square.Y += dy;

            if (square.Right >= bounds.Right || square.Left <= bounds.Left) dx = -dx;
            if (square.Bottom >= bounds.Bottom || square.Top <= bounds.Top) dy = -dy;

            Invalidate();

            using (var bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(this.BackColor);
                    g.FillRectangle(Brushes.Blue, square);
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    var bytes = ms.ToArray();
                    await BroadcastAsync(bytes);
                }
            }
        }

        private async Task BroadcastAsync(byte[] data)
        {
            List<TcpClient> snapshot;
            lock (clientsLock) snapshot = clients.ToList();

            foreach (var client in snapshot)
            {
                try
                {
                    if (!client.Connected)
                    {
                        lock (clientsLock) clients.Remove(client);
                        continue;
                    }

                    var stream = client.GetStream();
                    var lenBytes = BitConverter.GetBytes(data.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

                    await stream.WriteAsync(lenBytes, 0, lenBytes.Length);
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();
                }
                catch
                {
                    lock (clientsLock) clients.Remove(client);
                }
            }
        }

        private void Form1_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.Clear(this.BackColor);
            e.Graphics.FillRectangle(Brushes.Blue, square);

            lock (clientsLock)
            {
                e.Graphics.DrawString($"Clients: {clients.Count}", this.Font, Brushes.Black, 8, 8);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            drawTimer?.Stop();
            try { listener?.Stop(); } catch { }

            lock (clientsLock)
            {
                foreach (var c in clients) c.Close();
                clients.Clear();
            }
        }
    }
}
