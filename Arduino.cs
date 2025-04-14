using Godot;
using System;
using System.Collections.Generic;
using System.IO.Ports;

public partial class Arduino : Node2D
{
	private SerialPort serialPort;
	private RichTextLabel text;
	private TextureRect redScreen, blueScreen;
	private Label P1ScoreLabel, P2ScoreLabel, UIDLabel;
	private AudioStreamPlayer soundPlayer;

	private Dictionary<string, string> tagNameMap = new();
	private Dictionary<string, AudioStream> clueAudioMap = new();
	private Dictionary<string, AudioStream> secondClueAudioMap = new();
	private Dictionary<string, AudioStream> thirdClueAudioMap = new();

	private Dictionary<string, AudioStream> successAudioMap = new();
	private Dictionary<string, AudioStream> secondSuccessAudioMap = new();
	private Dictionary<string, AudioStream> thirdSuccessAudioMap = new();
	

	private List<string> preDecidedNames = new()
	{
		"shoulderSweaterMan", "trumpHat", "grandpaGrandson", "armyLady", "oldCouple",
		"purpleDress", "suitCaseMan", "lilGirl", "shortShortBlonde", "redGrayLady", "whiteTux", "caneGrandpa",
		"handHipsLady", "blondeChild", "blondeSuitCase", "whiteShirtMan", "blondeCouple", "redPantsLady", "yellowDress", "greenHoodie",
		"businessPocket", "blondeBrownShirt", "lilBoy", "blondeRedShirt", "yellowCoatLady", "yellowCamera", "baldBusiness", "professorGuy"
	};

	private List<string> specificUIDs = new()
	{
		"0x1D 0x71 0x86 0x6B 0x87 0x00 0x00", "0x1D 0xC5 0xF7 0x6B 0x87 0x00 0x00",
		"0x1D 0x37 0xC5 0x6B 0x87 0x00 0x00", "0x1D 0x0C 0xFD 0x6B 0x87 0x00 0x00",
		"0x1D 0xD8 0x74 0x6B 0x87 0x00 0x00", "0x1D 0xBC 0x71 0x6B 0x87 0x00 0x00",
		"0x1D 0xF5 0xFF 0x6B 0x87 0x00 0x00", "0x1D 0xB3 0xD8 0x6B 0x87 0x00 0x00",
		"0x1D 0xDF 0x95 0x6B 0x87 0x00 0x00", "0x1D 0x90 0x8F 0x6B 0x87 0x00 0x00",
		"0x1D 0x46 0x6B 0x6B 0x87 0x00 0x00", "0x1D 0xD8 0xEE 0x6B 0x87 0x00 0x00",
		"0x1D 0xB9 0x5F 0x6B 0x87 0x00 0x00", "0x1D 0x3C 0x99 0x6B 0x87 0x00 0x00",
		"0x1D 0x09 0x7E 0x6B 0x87 0x00 0x00", "0x1D 0xBE 0x71 0x6B 0x87 0x00 0x00",
		"0x1D 0xE7 0x7A 0x6B 0x87 0x00 0x00", "0x1D 0xD7 0x83 0x6B 0x87 0x00 0x00",
		"0x1D 0x8A 0xBF 0x6B 0x87 0x00 0x00", "0x1D 0xAE 0xEB 0x6B 0x87 0x00 0x00",
		"0x1D 0x63 0x71 0x6B 0x87 0x00 0x00", "0x1D 0x57 0xA9 0x6B 0x87 0x00 0x00",
		"0x1D 0x10 0xF5 0x6B 0x87 0x00 0x00", "0x1D 0x52 0x8C 0x6B 0x87 0x00 0x00",
		"0x1D 0x1B 0x62 0x6B 0x87 0x00 0x00", "0x1D 0xFB 0xA2 0x6B 0x87 0x00 0x00",
		"0x1D 0x99 0x68 0x6B 0x87 0x00 0x00", "0x1D 0xCC 0x9F 0x6B 0x87 0x00 0x00"
	};

	private List<string> ghostUIDs;
	private List<string> psychicUIDs;
	private List<string> bereavedUIDs;

	private string player1TargetTag;
	private string player2TargetTag;
	private int player1Stage = 0;
	private int player2Stage = 0;

	private RandomNumberGenerator rng = new();
	private int player1Score = 0, player2Score = 0;
	private bool isPlayer1Turn = true;
	private HashSet<string> usedTags = new();
	private bool gameStarted = false;

	private double lastScanTime = -2.0;
	private const double scanCooldown = 2.0;
	private string lastProcessedUID = "";
	private double lastUIDTime = 0;

	public override void _Ready()
	{
		text = GetNode<RichTextLabel>("RichTextLabel");
		redScreen = GetNode<TextureRect>("redScreen");
		blueScreen = GetNode<TextureRect>("blueScreen");
		P1ScoreLabel = GetNode<Label>("P1ScoreLabel");
		P2ScoreLabel = GetNode<Label>("P2ScoreLabel");
		UIDLabel = GetNode<Label>("UIDLabel");

		soundPlayer = new AudioStreamPlayer();
		AddChild(soundPlayer);

		serialPort = new SerialPort
		{
			PortName = "COM4",
			BaudRate = 115200,
			NewLine = "\n",
			DtrEnable = true
		};

		try
		{
			serialPort.Open();
			GD.Print("✔ Serial Port successfully opened.");
		}
		catch (Exception e)
		{
			GD.PrintErr("❌ Serial Open Fail: " + e.Message);
		}

		MapUIDsToNames();
		LoadClueAudio();
		LoadSecondClueAudio();
		LoadThirdClueAudio();
		LoadSuccessAudio();
		LoadThirdSuccessAudio();
		LoadSecondSuccessAudio();

		ghostUIDs = specificUIDs.GetRange(0, 8);
		psychicUIDs = specificUIDs.GetRange(8, 6);
		bereavedUIDs = specificUIDs.GetRange(14, 11);

		rng.Randomize();
		player1TargetTag = GetUniqueTag(ghostUIDs);
		player2TargetTag = GetUniqueTag(ghostUIDs);

		GetTree().CreateTimer(0.1f).Timeout += WaitForArduinoReady;
	}

	private async void WaitForArduinoReady()
	{
		GD.Print("⏳ Waiting for Arduino READY signal...");
		serialPort.WriteLine("Restart_Game");
		while (serialPort.IsOpen)
		{
			try
			{
				if (serialPort.BytesToRead > 0)
				{
					string msg = serialPort.ReadLine().Trim();
					GD.Print("📨 Received from Arduino: " + msg);
					if (msg == "READY")
					{
						GD.Print("✅ Sending ACK to Arduino...");
						serialPort.WriteLine("ACK");
						serialPort.WriteLine("TURN_RED");
						StartGame();
						//return;
					}
				}
			}
			catch (Exception e) { GD.PrintErr("Serial read error: " + e.Message); }
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
		}
	}

	private void StartGame()
	{
		isPlayer1Turn = true;
		text.Text = "Game Started";
		SetTurnVisibility();
		UpdateScoreLabels();
		PlayClueForCurrentTag();
		gameStarted = true;
		GD.Print("🎮 Game started: Player 1's turn.");
	}

	private void SetTurnVisibility()
	{
		redScreen.Visible = isPlayer1Turn;
		blueScreen.Visible = !isPlayer1Turn;
	}

	private void UpdateScoreLabels()
	{
		P1ScoreLabel.Text = $"Player 1: {player1Score}";
		P2ScoreLabel.Text = $"Player 2: {player2Score}";
	}

	private string GetUniqueTag(List<string> tagList)
	{
		string tag;
		do {
			tag = tagList[rng.RandiRange(0, tagList.Count - 1)];
		} while (usedTags.Contains(tag));
		usedTags.Add(tag);
		return tag;
	}

	private void DeclareWinner(string player)
	{
		text.Text = $"{player} wins!";
		GD.Print($"{player} wins!");
	}

	public override async void _Process(double delta)
	{
		if (!gameStarted || !serialPort.IsOpen) return;

		double currentTime = Time.GetTicksMsec() / 1000.0;
		if (currentTime - lastScanTime < scanCooldown) return;

		try
		{
			if (serialPort.BytesToRead > 0)
			{
				string tagID = serialPort.ReadLine().Trim();
				lastScanTime = currentTime;
				double now = Time.GetTicksMsec() / 1000.0;

				if (!tagID.StartsWith("0x")) 
					return;
				
				if (tagID == lastProcessedUID && now - lastUIDTime < 2.0)
					return;

				UIDLabel.Text = tagNameMap.ContainsKey(tagID) ? $"Scanned Tag: {tagNameMap[tagID]}" : "Unknown Tag";

				string currentTarget = isPlayer1Turn ? player1TargetTag : player2TargetTag;
				bool isCorrect = tagID == currentTarget;

				if (isCorrect)
				{
					text.Text = "Correct Tag! +1 Point";

					if (serialPort.IsOpen) serialPort.WriteLine("GREEN");

					if (isPlayer1Turn)
					{
						player1Score++;
						PlayClue1AnswerLine(tagID, player1Stage);
						await ToSignal(soundPlayer, "finished");
						player1Stage++;
						if (player1Stage == 1) 
						{
							player1TargetTag = GetUniqueTag(psychicUIDs);
						}
						else if (player1Stage == 2) 
						{
							player1TargetTag = GetUniqueTag(bereavedUIDs);
						}
						else {
							DeclareWinner("Player 1");
						}
					}
					else
					{
						player2Score++;
						PlayClue1AnswerLine(tagID, player2Stage);
						await ToSignal(soundPlayer, "finished");
						player2Stage++;
						if (player2Stage == 1) {
							player2TargetTag = GetUniqueTag(psychicUIDs);
							}
						else if (player2Stage == 2) 
						{
							player2TargetTag = GetUniqueTag(bereavedUIDs);
						}
						else {
							DeclareWinner("Player 2");
						}
					}

					UpdateScoreLabels();
				}
				else text.Text = "Wrong Tag";

				isPlayer1Turn = !isPlayer1Turn;
				SetTurnVisibility();
				PlayClueForCurrentTag();

				if (serialPort.IsOpen)
					serialPort.WriteLine(isPlayer1Turn ? "TURN_RED" : "TURN_BLUE");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr("Serial Read Error: " + e.Message);
		}
	}

	private void MapUIDsToNames()
	{
		for (int i = 0; i < specificUIDs.Count && i < preDecidedNames.Count; i++)
			tagNameMap[specificUIDs[i]] = preDecidedNames[i];
	}

private void LoadClueAudio()
{
	clueAudioMap = new Dictionary<string, AudioStream>
	{
		{ "shoulderSweaterMan", GD.Load<AudioStream>("res://assets/sounds for Capstone/SSM_Intro.mp3") },
		{ "trumpHat", GD.Load<AudioStream>("res://assets/sounds for Capstone/TH_Intro.mp3") },
		{ "grandpaGrandson", GD.Load<AudioStream>("res://assets/sounds for Capstone/GG-Intro.mp3") },
		{ "armyLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/AL_Intro.mp3") },
		{ "oldCouple", GD.Load<AudioStream>("res://assets/sounds for Capstone/OC_Intro.mp3") },
		{ "purpleDress", GD.Load<AudioStream>("res://assets/sounds for Capstone/PD_Intro.mp3") },
		{ "suitCaseMan", GD.Load<AudioStream>("res://assets/sounds for Capstone/SCM_Intro.mp3") },
		{ "lilGirl", GD.Load<AudioStream>("res://assets/sounds for Capstone/LG_Intro.mp3") }
	};
	
}

private void LoadSecondClueAudio()
{
	secondClueAudioMap = new Dictionary<string, AudioStream>
	{
		{ "shortShortBlonde", GD.Load<AudioStream>("res://assets/sounds for Capstone/ThongIsland_Speel.mp3") },
		{ "redGrayLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/Ronan_Intro.mp3") },
		{ "whiteTux", GD.Load<AudioStream>("res://assets/sounds for Capstone/Agatha_Intro.mp3") },
		{ "caneGrandpa", GD.Load<AudioStream>("res://assets/sounds for Capstone/Anthorpe_Intro.mp3") },
		{ "handHipsLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/Clementine_Intro.mp3") },
		{ "blondeChild", GD.Load<AudioStream>("res://assets/sounds for Capstone/Skittles_Intro.mp3") }
	};
}

private void LoadThirdClueAudio()
{
	thirdClueAudioMap = new Dictionary<string, AudioStream>
	{
		{ "blondeSuitCase", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_blondehair.mp3") },
		{ "whiteShirtMan", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_whiteShirt.mp3") },
		{ "blondeCouple", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_blondehair.mp3") },
		{ "redPantsLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_redPants.mp3") },
		{ "yellowDress", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_yellowDress.mp3") },
		{ "greenHoodie", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_greenHoodie.mp3") },
		{ "businessPocket", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_handsPocket.mp3") },
		{ "blondeBrownShirt", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_brownShirt.mp3") },
		{ "lilBoy", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_basketball.mp3") },
		{ "blondeRedShirt", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_blondehair.mp3") },
		{ "yellowCoatLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_yellowCoat.mp3") },
		{ "yellowCamera", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_camera.mp3") },
		{ "baldBusiness", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_baldHead.mp3") },
		{ "professorGuy", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_greenVest.mp3") }
	};
}

private void LoadSuccessAudio()
{
	successAudioMap = new Dictionary<string, AudioStream>
	{
		{ "shoulderSweaterMan", GD.Load<AudioStream>("res://assets/sounds for Capstone/Male_Next.mp3") },
		{ "trumpHat", GD.Load<AudioStream>("res://assets/sounds for Capstone/Male_Next.mp3") },
		{ "grandpaGrandson", GD.Load<AudioStream>("res://assets/sounds for Capstone/Child_Next.mp3") },
		{ "armyLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/Woman_Next.mp3") },
		{ "oldCouple", GD.Load<AudioStream>("res://assets/sounds for Capstone/Male_Next.mp3") },
		{ "purpleDress", GD.Load<AudioStream>("res://assets/sounds for Capstone/Woman_Next.mp3") },
		{ "suitCaseMan", GD.Load<AudioStream>("res://assets/sounds for Capstone/Male_Next.mp3") },
		{ "lilGirl", GD.Load<AudioStream>("res://assets/sounds for Capstone/Child_Next.mp3") }
	};
}

private void LoadSecondSuccessAudio()
{
	secondSuccessAudioMap = new Dictionary<string, AudioStream>
	{
		{ "shortShortBlonde", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_yellowCoat.mp3") },
		{ "redGrayLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_redPants.mp3") },
		{ "whiteTux", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_handsPocket.mp3") },
		{ "caneGrandpa", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_greenVest.mp3") },
		{ "handHipsLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_greenHoodie.mp3") },
		{ "blondeChild", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_camera.mp3") }
	};
}

private void LoadThirdSuccessAudio()
{
	thirdSuccessAudioMap = new Dictionary<string, AudioStream>
	{
		{ "blondeSuitCase", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_blondehair.mp3") },
		{ "whiteShirtMan", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_whiteShirt.mp3") },
		{ "blondeCouple", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_blondehair.mp3") },
		{ "redPantsLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_redPants.mp3") },
		{ "yellowDress", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_yellowDress.mp3") },
		{ "greenHoodie", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_greenHoodie.mp3") },
		{ "businessPocket", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_handsPocket.mp3") },
		{ "blondeBrownShirt", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_brownShirt.mp3") },
		{ "lilBoy", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_basketball.mp3") },
		{ "blondeRedShirt", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_blondehair.mp3") },
		{ "yellowCoatLady", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_yellowCoat.mp3") },
		{ "yellowCamera", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_camera.mp3") },
		{ "baldBusiness", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_baldHead.mp3") },
		{ "professorGuy", GD.Load<AudioStream>("res://assets/sounds for Capstone/clue_greenVest.mp3") }
	};
}

private void PlayClue1AnswerLine(string tagID, int stage)
{
	if (!tagNameMap.TryGetValue(tagID, out var tagName)) return;

	AudioStream stream = stage switch
	{
		0 => successAudioMap.GetValueOrDefault(tagName),
		1 => secondSuccessAudioMap.GetValueOrDefault(tagName),
		2 => thirdSuccessAudioMap.GetValueOrDefault(tagName),
		_ => null
	};

	if (stream != null)
	{
		soundPlayer.Stream = stream;
		soundPlayer.Play();
		GD.Print($"🔊 Playing success line for: {tagName}");
	}
	else
	{
		GD.PrintErr($"❌ No success audio found for {tagName} at stage {stage}");
	}
}


private void PlayClueForCurrentTag()
{
	string tagID = isPlayer1Turn ? player1TargetTag : player2TargetTag;

	if (!tagNameMap.TryGetValue(tagID, out var tagName)) return;

	int stage = isPlayer1Turn ? player1Stage : player2Stage;
	AudioStream stream = stage switch
	{
		0 => clueAudioMap.GetValueOrDefault(tagName),
		1 => secondClueAudioMap.GetValueOrDefault(tagName),
		2 => thirdClueAudioMap.GetValueOrDefault(tagName),
		_ => null
	};

	if (stream != null)
	{
		soundPlayer.Stream = stream;
		soundPlayer.Play();
		GD.Print($"🔊 Playing clue for {tagName} (stage {stage})");
	}
	else
	{
		GD.PrintErr($"❌ No clue audio found for {tagName} at stage {stage}");
	}
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
