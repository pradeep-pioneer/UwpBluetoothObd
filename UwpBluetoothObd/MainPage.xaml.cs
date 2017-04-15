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
using Windows.Devices.SerialCommunication;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Core;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UwpBluetoothObd
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DeviceInformationCollection deviceCollection;
        private DeviceInformation selectedDevice;
        private RfcommDeviceService deviceService;
        CoreDispatcher dispatcher = Window.Current.Dispatcher;
        public string deviceName = "HUAWEI KII-L22";

        StreamSocket streamSocket = new StreamSocket();
        private async void InitializeRfcommServer()
        {
            try
            {
                listBoxDevices.Text = string.Empty;
                string device1 = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                deviceCollection = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(device1);
                selectedDevice = deviceCollection[0];
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    foreach (var item in deviceCollection)
                    {
                        listBoxDevices.Text += "\n========================";
                        listBoxDevices.Text += "\nDevice";
                        listBoxDevices.Text += string.Format("\nName: {0}\nId: {1}", item.Name, item.Id);
                        listBoxDevices.Text += "\n========================";
                        listBoxDevices.Text += "\nProperties";
                        listBoxDevices.Text += "\n========================";
                        foreach (var prop in item.Properties)
                        {
                            listBoxDevices.Text += string.Format("\nName: {0} - \tValue: {1}", prop.Key, prop.Value);
                        }
                        listBoxDevices.Text += "\n========================";
                    }
                });
                deviceService = await RfcommDeviceService.FromIdAsync(selectedDevice.Id);
                if (deviceService != null)
                {
                    //connect the socket   
                    try
                    {
                        await streamSocket.ConnectAsync(deviceService.ConnectionHostName, deviceService.ConnectionServiceName);
                    }
                    catch (Exception ex)
                    {
                        errorStatus.Visibility = Visibility.Visible;
                        errorStatus.Text = "Cannot connect bluetooth device:" + ex.Message;
                    }

                }
                else
                {
                    errorStatus.Visibility = Visibility.Visible;
                    errorStatus.Text = "Didn't find the specified bluetooth device";
                }
            }
            catch (Exception exception)
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = exception.Message;
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            
        }

        private void buttonDiscover_Click(object sender, RoutedEventArgs e)
        {
            InitializeRfcommServer();
        }

        private async void buttonSend_Click(object sender, RoutedEventArgs e)
        {
            if (deviceService != null)
            {
                //send data
                string sendData = "ATZ\r";
                if (string.IsNullOrEmpty(sendData))
                {
                    errorStatus.Visibility = Visibility.Visible;
                    errorStatus.Text = "Please specify the string you are going to send";
                }
                else
                {
                    DataWriter dwriter = new DataWriter(streamSocket.OutputStream);
                    dwriter.WriteString(sendData);
                    await dwriter.StoreAsync();
                    await dwriter.FlushAsync();
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    AddLog(string.Format("Request: {0}", sendData));
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    
                    sendData = "ATSP6\r";
                    dwriter.WriteString(sendData);
                    await dwriter.StoreAsync();
                    await dwriter.FlushAsync();
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    AddLog(string.Format("Request: {0}", sendData));
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    sendData = "ATH0\r";
                    dwriter.WriteString(sendData);
                    await dwriter.StoreAsync();
                    await dwriter.FlushAsync();
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    AddLog(string.Format("Request: {0}", sendData));
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    sendData = "ATCAF1\r";
                    dwriter.WriteString(sendData);
                    await dwriter.StoreAsync();
                    await dwriter.FlushAsync();
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    AddLog(string.Format("Request: {0}", sendData));
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    sendData = "01 0D\r";
                    dwriter.WriteString(sendData);
                    await dwriter.StoreAsync();
                    await dwriter.FlushAsync();
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    AddLog(string.Format("Request: {0}", sendData));
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                    ReadData();
                }

            }
            else
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = "Bluetooth is not connected correctly!";
            }
        }

        private async void ReadData()
        {
            DataReader dreader = new DataReader(streamSocket.InputStream);
            dreader.InputStreamOptions = InputStreamOptions.Partial;
            uint count = await dreader.LoadAsync(512);
            string text = dreader.ReadString(count);
            AddLog(text);
        }

        private async void ReceiveData()
        {
            // read the data

            DataReader dreader = new DataReader(streamSocket.InputStream);
            uint sizeFieldCount = await dreader.LoadAsync(sizeof(uint));
            if (sizeFieldCount != sizeof(uint))
            {
                return;
            }

            uint stringLength;
            uint actualStringLength;

            try
            {
                stringLength = dreader.ReadUInt32();
                actualStringLength = await dreader.LoadAsync(stringLength);

                if (stringLength != actualStringLength)
                {
                    return;
                }
                string text = dreader.ReadString(actualStringLength);
                AddLog(string.Format("Response: {0}", text));
            }
            catch (Exception ex)
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = "Reading data from Bluetooth encountered error!" + ex.Message;
            }
        }

        private async void AddLog(string text)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                listBoxObdCommunication.Items.Add(text);
            });
        }
    }
}
