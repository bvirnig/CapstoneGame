using Godot;
using System;
using System.IO.Ports;
using System.Collections.Generic;

public partial class Arduino : Node2D
{
	SerialPort serialPort;
	RichTextLabel text;

	// Dictionary to store RFID tag UIDs and their corresponding short names
	private Dictionary<string, string> tagNameMap = new Dictionary<string, string>();

	// Pre-decided list of short names (these should match positions with the UIDs you want to assign)
	private List<string> preDecidedNames = new List<string>
	{
		"P1_R1",
		 "P1_R2",
		 "P1_R3",
		 "P1_R4",
		 "P1_R5",
		 "P1_R6",
		 "P1_R7",
		 "P1_R8",
		 "P1_R9",
		 "P1_R10" 
	};

	// List of specific UIDs to match with the pre-decided names
	private List<string> specificUIDs = new List<string>
	{
		"0x1D 0x0C 0xFD 0x6B 0x87 0x00 0x00", 
		"0x1D 0xAE 0xEB 0x6B 0x87 0x00 0x00", 
		"0x1D 0x06 0xCF 0x6B 0x87 0x00 0x00", 
		"0x1D 0xF5 0xFF 0x6B 0x87 0x00 0x00", 
		"0x1D 0xBC 0x71 0x6B 0x87 0x00 0x00",
		"0x1D 0x10 0xF5 0x6B 0x87 0x00 0x00",//
		"0x1D 0x8A 0xBF 0x6B 0x87 0x00 0x00",
		"0x1D 0x37 0xC5 0x6B 0x87 0x00 0x00",
		"0x1D 0xD8 0xEE 0x6B 0x87 0x00 0x00",
		"0x1D 0xB3 0xD8 0x6B 0x87 0x00 0x00", // Example UIDs, replace with actual UIDs
	};

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		text = GetNode<RichTextLabel>("RichTextLabel");

		// Initialize the serial port
		serialPort = new SerialPort();
		serialPort.PortName = "COM6";  // Ensure the correct port is selected
		serialPort.BaudRate = 115200;  // Match with Arduino baud rate
		serialPort.NewLine = "\n";  // Ensure newline character is set correctly
		serialPort.DtrEnable = true;

		try
		{
			serialPort.Open();
			GD.Print("Serial Port Opened Successfully.");
		}
		catch (Exception e)
		{
			GD.PrintErr("Failed to open serial port: " + e.Message);
		}

		// Clear the text at the start
		text.Text = "Waiting for RFID scan...";

		// Map the specific UIDs to the pre-decided short names at the start
		MapUIDsToNames();
	}

	// Method to map specific UIDs to their corresponding names
	private void MapUIDsToNames()
	{
		for (int i = 0; i < specificUIDs.Count; i++)
		{
			if (i < preDecidedNames.Count)
			{
				string uid = specificUIDs[i];
				string name = preDecidedNames[i];
				tagNameMap[uid] = name;
				GD.Print($"Mapped UID: {uid} to Name: {name}");
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!serialPort.IsOpen)
		{
			GD.PrintErr("Serial Port is not open!");
			return;
		}

		// Try to read a line of data from the serial port
		try
		{
			if (serialPort.BytesToRead > 0)
			{
				string serialMessage = serialPort.ReadLine();  // Wait for the next line of data
				GD.Print("Received serial message (UID): " + serialMessage);  // Print the UID (RFID tag ID)

				// Assuming the message is the RFID tag ID (e.g., "123456789")
				string tagID = serialMessage.Trim();  // Trim any extra whitespace or newline

				// Check if the tag already has a short name
				if (tagNameMap.ContainsKey(tagID))
				{
					// If the tag already has a name, show it
					string shortName = tagNameMap[tagID];
					GD.Print($"Tag ID: {tagID} is assigned to {shortName}");
					text.Text = $"Tag ID: {tagID} is assigned to {shortName}";
				}
				else
				{
					// If the tag doesn't have a name, show an error
					GD.PrintErr($"No short name found for Tag ID: {tagID}");
					text.Text = $"No name found for Tag ID: {tagID}";
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Error reading serial data: " + e.Message);
		}
	}

	// Ensure we close the port when the app is closed or changed
	public override void _ExitTree()
	{
		if (serialPort.IsOpen)
		{
			serialPort.Close();
			GD.Print("Serial Port Closed.");
		}
	}
}
