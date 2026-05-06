using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace SerialCommunication
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			try
			{
				string[] portNames = SerialPort.GetPortNames().Distinct().ToArray();
				comboBoxPoort.Items.Clear();
				comboBoxPoort.Items.AddRange(portNames);
				if (comboBoxPoort.Items.Count > 0) comboBoxPoort.SelectedIndex = 0;

				comboBoxBaudrate.SelectedIndex = comboBoxBaudrate.Items.IndexOf("115200");
			}
			catch (Exception)
			{ }
		}

		private void cboPoort_DropDown(object sender, EventArgs e)
		{
			try
			{
				string selected = (string)comboBoxPoort.SelectedItem;
				string[] portNames = SerialPort.GetPortNames().Distinct().ToArray();

				comboBoxPoort.Items.Clear();
				comboBoxPoort.Items.AddRange(portNames);

				comboBoxPoort.SelectedIndex = comboBoxPoort.Items.IndexOf(selected);
			}
			catch (Exception)
			{
				if (comboBoxPoort.Items.Count > 0) comboBoxPoort.SelectedIndex = 0;
			}
		}

		private void buttonConnect_Click(object sender, EventArgs e)
		{
			try
			{
				if (serialPortArduino.IsOpen)
				{
					// ik heb een verbinding -> de gebruiker wil deze verbreken
					serialPortArduino.Close();
					radioButtonVerbonden.Checked = false;
					buttonConnect.Text = "Connect";
					labelStatus.Text = "Status: Disconnected";
				}
				else
				{
					//ik heb geen verbinding -> de gebruiker wil verbinding maken
					serialPortArduino.PortName = (string)comboBoxPoort.SelectedItem;
					serialPortArduino.BaudRate = Int32.Parse((string)comboBoxBaudrate.SelectedItem);
					serialPortArduino.DataBits = (int)numericUpDownDatabits.Value;

					if (radioButtonParityEven.Checked) serialPortArduino.Parity = Parity.Even;
					else if (radioButtonParityOdd.Checked) serialPortArduino.Parity = Parity.Odd;
					else if (radioButtonParityNone.Checked) serialPortArduino.Parity = Parity.None;
					else if (radioButtonParityMark.Checked) serialPortArduino.Parity = Parity.Mark;
					else if (radioButtonParitySpace.Checked) serialPortArduino.Parity = Parity.Space;

					if (radioButtonStopbitsNone.Checked) serialPortArduino.StopBits = StopBits.None;
					else if (radioButtonStopbitsOne.Checked) serialPortArduino.StopBits = StopBits.One;
					else if (radioButtonStopbitsOnePointFive.Checked) serialPortArduino.StopBits = StopBits.OnePointFive;
					else if (radioButtonStopbitsTwo.Checked) serialPortArduino.StopBits = StopBits.Two;


					if (radioButtonHandshakeNone.Checked) serialPortArduino.Handshake = Handshake.None;
					else if (radioButtonHandshakeRTS.Checked) serialPortArduino.Handshake = Handshake.RequestToSend;
					else if (radioButtonHandshakeRTSXonXoff.Checked) serialPortArduino.Handshake = Handshake.RequestToSendXOnXOff;
					else if (radioButtonHandshakeXonXoff.Checked) serialPortArduino.Handshake = Handshake.XOnXOff;

					serialPortArduino.RtsEnable = checkBoxRtsEnable.Checked;
					serialPortArduino.DtrEnable = checkBoxDtrEnable.Checked;

					serialPortArduino.Open();
					string commando = "ping";
					serialPortArduino.WriteLine(commando);
					string antwoord = serialPortArduino.ReadLine();
					antwoord = antwoord.TrimEnd();
					if (antwoord == "pong")
					{
						radioButtonVerbonden.Checked = true;
						buttonConnect.Text = "Disconnect";
						labelStatus.Text = "Status: Connected";
					}
					else
					{
						serialPortArduino.Close();
						labelStatus.Text = "Error: Verkeerd Antwoord";

					}



				}
			}
			catch (Exception exception)
			{
				labelStatus.Text = "Error: " + exception.Message;
				serialPortArduino.Close();
				radioButtonVerbonden.Checked = false;
				buttonConnect.Text = "Connect";
			}

		}
		private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Start the timer ONLY if Oefening 5 is open
			if (tabControl.SelectedTab == tabPageOefening5)
			{
				timerOefening5.Start();
			}
			else
			{
				timerOefening5.Stop();
			}

		}
		private void timerOefening5_Tick(object sender, EventArgs e)
		{
			if (!serialPortArduino.IsOpen) return;

			try
			{
				// --- 1. Clean out any old garbage in the wire before asking
				serialPortArduino.DiscardInBuffer();

				// --- 2. Vraag potentiometer (gewenste temp)
				serialPortArduino.WriteLine("get a0");
				Thread.Sleep(50);
				string lijn1 = serialPortArduino.ReadLine();

				// IF there is no colon, ignore it and stop trying for this second!
				if (!lijn1.Contains(":")) return;

				int potWaarde = int.Parse(lijn1.Split(':')[1].Trim());

				// --- 3. Vraag LM35 (huidige temp)
				serialPortArduino.WriteLine("get a1");
				Thread.Sleep(50);
				string lijn2 = serialPortArduino.ReadLine();

				// IF there is no colon, ignore it and stop trying for this second!
				if (!lijn2.Contains(":")) return;

				int tempWaarde = int.Parse(lijn2.Split(':')[1].Trim());

				// --- 4. Verwerken
				VerwerkData(potWaarde, tempWaarde);
			}
			catch (Exception ex)
			{
				// voorkomt crash
				timerOefening5.Stop();
				MessageBox.Show("Error: " + ex.Message);

			}
		}
		private void VerwerkData(int potWaarde, int tempWaarde)
		{
			// --- GEWENSTE TEMPERATUUR (5 → 45 °C)
			double gewensteTemp = (40.0 / 1023.0) * potWaarde + 5;

			// --- HUIDIGE TEMPERATUUR (LM35 → 0 → 500 °C)
			double huidigeTemp = (500.0 / 1023.0) * tempWaarde;

			// --- TONEN OP SCHERM (1 cijfer na komma)
			labelGewensteTemp.Text = gewensteTemp.ToString("0.0") + " °C";
			labelHuidigeTemp.Text = huidigeTemp.ToString("0.0") + " °C";

			// --- LED LOGICA
			if (huidigeTemp < gewensteTemp)
			{
				serialPortArduino.WriteLine("set d2 1"); // LED AAN
			}
			else
			{
				serialPortArduino.WriteLine("set d2 0"); // LED UIT
			}
		}
	}
}
