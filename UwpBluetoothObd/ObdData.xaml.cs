using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using System.Threading.Tasks;
//Author: Pradeep Singh

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UwpBluetoothObd
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ObdData : Page
    {
        CoreDispatcher dispatcher = Window.Current.Dispatcher;

        private DeviceInformationCollection deviceCollection;
        private DeviceInformation selectedDevice;
        private RfcommDeviceService deviceService;

        StreamSocket streamSocket = new StreamSocket();
        DataReader reader;
        DataWriter writer;
        public ObdData()
        {
            this.InitializeComponent();
        }

        private async Task LogStatus(string status)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Status.Text = string.Format("Status: {0}", status);
            });
        }

        private async Task AddCommandLog(string value, bool isResponse = false)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Commands.Items.Add(string.Format("{0}: {1}", isResponse?"Response":"Request", value));
                Commands.SelectedIndex = Commands.Items.Count - 1;
                Commands.ScrollIntoView(Commands.SelectedItem);
            });
        }
        private async Task SetupConnections()
        {
            string device1 = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
            deviceCollection = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(device1);
            await AddCommandLog(string.Format("Connecting to : {0}", device1));
            await AddCommandLog(string.Format("Devices found : {0}", deviceCollection.Count));
            selectedDevice = deviceCollection[0];
            deviceService = await RfcommDeviceService.FromIdAsync(selectedDevice.Id);
            if (deviceService != null)
            {
                await streamSocket.ConnectAsync(deviceService.ConnectionHostName, deviceService.ConnectionServiceName);
                await LogStatus("Connection Established!");
                await SendInitializationCommands();
                await SetupOptions(true);
                await LogStatus("Ready for command");
            }
            else
            {
                await SetupOptions(false);
                throw new Exception("Didn't find the specified bluetooth device");
            }
        }
        private void SetupStreams()
        {
            reader = new DataReader(streamSocket.InputStream);
            reader.InputStreamOptions = InputStreamOptions.Partial;
            writer = new DataWriter(streamSocket.OutputStream);
        }
        private async Task SendInitializationCommands()
        {
            string response = string.Empty;
            await LogStatus("Setting Up Streams!");
            SetupStreams();
            await LogStatus("Streams Setup!");
            await LogStatus("Sending Commands!");
            await AddCommandLog("ATZ\r");
            response = await SendCommand("ATZ\r");
            await AddCommandLog(response, true);

            await AddCommandLog("ATSP6\r");
            response = await SendCommand("ATSP6\r");
            await AddCommandLog(response, true);

            await AddCommandLog("ATH0\r");
            response = await SendCommand("ATH0\r");
            await AddCommandLog(response, true);

            await AddCommandLog("ATCAF1\r");
            response = await SendCommand("ATCAF1\r");
            await AddCommandLog(response, true);
        }

        private async Task<string> SendCommand(string command)
        {
            await LogStatus(string.Format("Sending Command - {0}", command));
            writer.WriteString(command);
            await writer.StoreAsync();
            await writer.FlushAsync();
            uint count = await reader.LoadAsync(512);
            return reader.ReadString(count).Trim('>');
        }

        private async Task SetupOptions(bool enabled)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Speed.IsEnabled = enabled;
                Rpm.IsEnabled = enabled;
                EngineLoad.IsEnabled = enabled;
            });
        }

        private async Task InitializeStuff()
        {
            try
            {
                await LogStatus("Initialization Started!");
                await SetupConnections();
            }
            catch(Exception ex)
            {
                await LogStatus(string.Format("Exception: {0}", ex.Message));
                await SetupOptions(false);
            }
        }

        private async void Initialize_Click(object sender, RoutedEventArgs e)
        {
            await InitializeStuff();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await SetupOptions(false);
        }

        private int decodeHexNumber(string hexString)
        {
            byte[] raw = new byte[hexString.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            string final = raw[0].ToString();
            AddCommandLog(final, true);
            return Convert.ToInt32(final);
        }

        private async void Speed_Click(object sender, RoutedEventArgs e)
        {
            await AddCommandLog("010D\r");
            var response = await SendCommand("010D\r");
            await AddCommandLog(response, true);
            await DecodeAndShowSpeed(response);
            await LogStatus("Ready for command");
        }

        private async Task DecodeAndShowSpeed(string response)
        {
            var speedHexString = response.Substring(4);
            int speed = decodeHexNumber(speedHexString);
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RadialGaugeSpeed.Value = speed;
            });
        }

        private async void Rpm_Click(object sender, RoutedEventArgs e)
        {
            await AddCommandLog("010C\r");
            var response = await SendCommand("010C\r");
            await AddCommandLog(response, true);
            await DecodeAndShowRpm(response);
            await LogStatus("Ready for command");
        }

        private async Task DecodeAndShowRpm(string response)
        {
            var rpmString = response.Substring(4);
            double rpm = (Convert.ToDouble(decodeHexNumber(response.Substring(4))));
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RadialGaugeRpm.Value = rpm;
            });
        }

        private async void EngineLoad_Click(object sender, RoutedEventArgs e)
        {
            await AddCommandLog("0104\r");
            var response = await SendCommand("0104\r");
            await AddCommandLog(response, true);
            await DecodeAndShowEngineLoad(response);
            await LogStatus("Ready for command");
        }

        private async Task DecodeAndShowEngineLoad(string response)
        {
            var rpmHexString = response.Substring(4);
            double rpm = (100f / 255f) * (Convert.ToDouble(decodeHexNumber(rpmHexString)));
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RadialGaugeEngineLoad.Value = rpm;
            });
        }
    }
}
