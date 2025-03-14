using Godot;
using System;
using System.IO.Ports;
using System.Collections.Generic;

public partial class Arduino : Node2D
{
	SerialPort serialPort;
	RichTextLabel text;

	// Create references to the TextureRect nodes
	private TextureRect redScreen;
	private TextureRect blueScreen;

	// Create references to the score labels
	private Label P1ScoreLabel;
	private Label P2ScoreLabel;

	// Create reference to the UIDLabel (now will show pre-decided name)
	private Label UIDLabel;

	// Create an AudioStreamPlayer to play the sound
	private AudioStreamPlayer soundPlayer;

	// Dictionary to store RFID tag UIDs and their corresponding short names
	private Dictionary<string, string> tagNameMap = new Dictionary<string, string>();

	// Pre-decided list of short names (these should match positions with the UIDs you want to assign)
	private List<string> preDecidedNames = new List<string>
	{
		"P1_R1", "P1_R2", "P1_R3", "P1_R4", "P1_R5",
		"P1_R6", "P1_R7", "P1_R8", "P1_R9", "P1_R10"
	};

	// List of specific UIDs to match with the pre-decided names
	private List<string> specificUIDs = new List<string>
	{
		"0x1D 0x0C 0xFD 0x6B 0x87 0x00 0x00", "0x1D 0xAE 0xEB 0x6B 0x87 0x00 0x00",
		"0x1D 0x06 0xCF 0x6B 0x87 0x00 0x00", "0x1D 0xF5 0xFF 0x6B 0x87 0x00 0x00",
		"0x1D 0xBC 0x71 0x6B 0x87 0x00 0x00", "0x1D 0x10 0xF5 0x6B 0x87 0x00 0x00",
		"0x1D 0x8A 0xBF 0x6B 0x87 0x00 0x00", "0x1D 0x37 0xC5 0x6B 0x87 0x00 0x00",
		"0x1D 0xD8 0xEE 0x6B 0x87 0x00 0x00", "0x1D 0xB3 0xD8 0x6B 0x87 0x00 0x00"
	};

	// Audio files for each tag (match the tag array)
	private List<AudioStream> audioFiles = new List<AudioStream>
	{
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v2.mp3"),  // Sound for the first tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v3.mp3"),  // Sound for the second tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v4 (1).mp3"),  // Sound for the third tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v5.mp3"),  // Sound for the fourth tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v6 (1).mp3"),  // Sound for the fifth tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v7.mp3"),  // Sound for the sixth tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v8.mp3"),  // Sound for the seventh tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v9.mp3"),  // Sound for the eighth tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v10.mp3"),  // Sound for the ninth tag
		GD.Load<AudioStream>("res://assets/sounds/Project name (en) v11.mp3")  // Sound for the tenth tag
	};

	private RandomNumberGenerator rng = new RandomNumberGenerator();

	// Separate scores for both players
	private int player1Score = 0;
	private int player2Score = 0;

	private string currentExpectedTag;

	// Track which player's turn it is
	private bool isPlayer1Turn = true;

	public override void _Ready()
	{
		text = GetNode<RichTextLabel>("RichTextLabel");

		// Get references to the TextureRect nodes for redScreen and blueScreen
		redScreen = GetNode<TextureRect>("redScreen");
		blueScreen = GetNode<TextureRect>("blueScreen");

		// Get references to the score labels
		P1ScoreLabel = GetNode<Label>("P1ScoreLabel");
		P2ScoreLabel = GetNode<Label>("P2ScoreLabel");

		// Get reference to the UIDLabel (will now show pre-decided name)
		UIDLabel = GetNode<Label>("UIDLabel");

		// Create the AudioStreamPlayer
		soundPlayer = new AudioStreamPlayer();
		AddChild(soundPlayer);  // Add it as a child to the node tree so it can play sounds

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
		text.Text = "Please scan the expected tag...";

		// Map the specific UIDs to the pre-decided short names at the start
		MapUIDsToNames();

		// Set the first expected tag
		currentExpectedTag = GetRandomTag();
		GD.Print($"Please scan the expected tag: {currentExpectedTag}");

		// Play the sound at the beginning of the game
		PlayTagSound(currentExpectedTag);

		// Set visibility based on the current player's turn
		SetTurnVisibility();

		// Update the score labels initially
		UpdateScoreLabels();
	}

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

	private string GetRandomTag()
	{
		// Simply pick a random tag without checking if it was used before
		int randomIndex = rng.RandiRange(0, specificUIDs.Count - 1);
		string randomTag = specificUIDs[randomIndex];
		return randomTag;
	}

	private void PlayTagSound(string tagID)
	{
		// Get the sound index based on the tagID
		int tagIndex = specificUIDs.IndexOf(tagID);
		if (tagIndex >= 0 && tagIndex < audioFiles.Count)
		{
			soundPlayer.Stream = audioFiles[tagIndex];  // Set the audio file
			soundPlayer.Play();  // Play the sound
		}
	}

	public override void _Process(double delta)
	{
		if (!serialPort.IsOpen)
		{
			GD.PrintErr("Serial Port is not open!");
			return;
		}

		try
		{
			if (serialPort.BytesToRead > 0)
			{
				string serialMessage = serialPort.ReadLine();
				string tagID = serialMessage.Trim();

				// Get the pre-decided name for the scanned tag from the tagNameMap
				if (tagNameMap.ContainsKey(tagID))
				{
					string tagName = tagNameMap[tagID];
					UIDLabel.Text = $"Scanned Tag: {tagName}";  // Display the pre-decided name
				}
				else
				{
					UIDLabel.Text = "Unknown Tag";  // Display a fallback if the tag is not found
				}

				// Switch turn on every tag scan, regardless of correctness
				SwitchTurn();

				// If the tag is correct, increase score and update expected tag
				if (tagID == currentExpectedTag)
				{
					GD.Print($"Correct Tag! +1 Point.");
					if (isPlayer1Turn)
					{
						player1Score++;  // Increment Player 1's score
					}
					else
					{
						player2Score++;  // Increment Player 2's score
					}

					// Play the sound corresponding to the tag
					PlayTagSound(currentExpectedTag);

					// Update the score labels
					UpdateScoreLabels();

					// Update text with the current score of the player
					text.Text = $"Correct Tag! +1 Point.";

					// Set the next expected tag after correct scan
					currentExpectedTag = GetRandomTag();
					GD.Print($"Please scan the next expected tag: {currentExpectedTag}");

					// Play the sound for the next tag
					PlayTagSound(currentExpectedTag);
				}
				else
				{
					GD.Print("Wrong Tag");
					text.Text = "Wrong Tag";
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Error reading serial data: " + e.Message);
		}
	}

	// Switch player turn and toggle visibility of redScreen and blueScreen
	private void SwitchTurn()
	{
		isPlayer1Turn = !isPlayer1Turn;  // Toggle between Player 1 and Player 2
		SetTurnVisibility();
	}

	// Set the visibility of redScreen and blueScreen based on the player's turn
	private void SetTurnVisibility()
	{
		if (isPlayer1Turn)
		{
			redScreen.Visible = true;
			blueScreen.Visible = false;
		}
		else
		{
			redScreen.Visible = false;
			blueScreen.Visible = true;
		}
	}

	// Update the score labels with the current scores of both players
	private void UpdateScoreLabels()
	{
		P1ScoreLabel.Text = "Player 1: " + player1Score;
		P2ScoreLabel.Text = "Player 2: " + player2Score;
	}

	public override void _ExitTree()
	{
		if (serialPort.IsOpen)
		{
			serialPort.Close();
			GD.Print("Serial Port Closed.");
		}
	}
}
