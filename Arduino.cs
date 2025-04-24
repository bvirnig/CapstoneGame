using Godot;
using System;
using System.Collections.Generic;
using System.IO.Ports;

public partial class Arduino : Node2D
{
	private SerialPort serialPort;
	private RichTextLabel text;
	private TextureRect redScreen;
	private Label P1ScoreLabel, UIDLabel;
	private AudioStreamPlayer soundPlayer;

	private Dictionary<string, string> tagNameMap = new();
	private Dictionary<string, AudioStream> clueAudioMap = new();
	private Dictionary<string, AudioStream> secondClueAudioMap = new();
	private Dictionary<string, (AudioStream clue, string gender)> thirdClueAudioMap = new();

	private Dictionary<string, AudioStream> successAudioMap = new();
	private Dictionary<string, AudioStream> secondSuccessAudioMap = new();
	private Dictionary<string, Dictionary<string, AudioStream>> thirdSuccessAudioMap = new();
	
	private Dictionary<string, AudioStream> stage0RejectionMap = new();
	private Dictionary<string, AudioStream> stage1RejectionMap = new();
	private Dictionary<string, AudioStream> stage2RejectionMap = new();
	
	private Dictionary<string, string> characterGenderMap = new();


	private AudioStream gameStartAudio;
	private AudioStream gameEndAudio;
	private Dictionary<int, AudioStream> stageStartAudioMap = new();
	
	private AudioStream correctSFX;
	private AudioStream incorrectSFX;
	private AudioStreamPlayer sfxPlayer;


	

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
	private int player1Stage = 0;
	
	private string player1Stage3TargetGender;
	private string stage1GhostNameP1;

	private RandomNumberGenerator rng = new();
	private int player1Score = 0;
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
		P1ScoreLabel = GetNode<Label>("P1ScoreLabel");
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
		
		LoadRejectionAudio();
		LoadCharacterGenders();
		LoadStageIntroAudio();
		
		correctSFX = GD.Load<AudioStream>("res://assets/SFX/correct_beep.mp3");
		incorrectSFX = GD.Load<AudioStream>("res://assets/SFX/wrong_buzz.mp3");
		
		soundPlayer = new AudioStreamPlayer();
		AddChild(soundPlayer);

		// 🔔 SFX player for correct/wrong buzz sounds
		sfxPlayer = new AudioStreamPlayer();
		sfxPlayer.Name = "SFXPlayer";
		AddChild(sfxPlayer);




		ghostUIDs = specificUIDs.GetRange(0, 8);
		psychicUIDs = specificUIDs.GetRange(8, 6);
		bereavedUIDs = specificUIDs.GetRange(14, 11);

		rng.Randomize();
		player1TargetTag = GetUniqueTag(ghostUIDs);
		stage1GhostNameP1 = tagNameMap[player1TargetTag];

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
						StartGame();
						//return;
					}
				}
			}
			catch (Exception e) { GD.PrintErr("Serial read error: " + e.Message); }
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
		}
	}

private async void StartGame()
{
	isPlayer1Turn = true;
	text.Text = "Game Started";
	SetTurnVisibility();
	UpdateScoreLabels();
	gameStarted = true;
	GD.Print("🎮 Game started: Player 1's turn.");

	// 🟡 Send yellow light for game start intro
	if (serialPort.IsOpen)
	{
		serialPort.WriteLine("YELLOW");
		GD.Print("🟡 Sent YELLOW command for game start intro");
	}

	await ToSignal(GetTree().CreateTimer(0.2f), "timeout"); // Short buffer

	// 🎧 Play the game start line
	if (gameStartAudio != null)
	{
		if (serialPort.IsOpen)
		{
			serialPort.WriteLine("RAINBOW");
			GD.Print("🌈 Sent RAINBOW command for game start");

			serialPort.WriteLine("RAINBOWRING");
			GD.Print("🌈 Sent RAINBOWRING command for game start");
		}

		soundPlayer.Stream = gameStartAudio;
		soundPlayer.Play();
		GD.Print("🔊 Game start line played");
		await ToSignal(soundPlayer, "finished");
	}

	// 🟡 Send yellow before stage 0 intro
	if (serialPort.IsOpen)
	{
		serialPort.WriteLine("YELLOW:13000");
		GD.Print("🟡 Sent YELLOW command for stage 0 intro");
	}

	await ToSignal(GetTree().CreateTimer(0.2f), "timeout"); // Buffer so yellow shows before audio

	// 🎧 Play the stage 0 intro
	if (stageStartAudioMap.TryGetValue(0, out var stage0Intro))
	{
		soundPlayer.Stream = stage0Intro;
		soundPlayer.Play();
		GD.Print("🔊 Stage 0 intro played");
		await ToSignal(soundPlayer, "finished");
	}

	// 🕵️‍♂️ Play first clue
	PlayClueForCurrentTag();
}




	private void SetTurnVisibility()
	{
		redScreen.Visible = isPlayer1Turn;
	}

	private void UpdateScoreLabels()
	{
		P1ScoreLabel.Text = $"Player 1: {player1Score}";
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

private async void DeclareWinner(string player)
{
	text.Text = $"{player} wins!";
	GD.Print($"{player} wins!");

	if (serialPort.IsOpen)
	{
		serialPort.WriteLine("RAINBOW");
		GD.Print("🌈 Sent RAINBOW command for game end");

		serialPort.WriteLine("RAINBOWRING");
		GD.Print("🌈 Sent RAINBOWRING command for game end");
	}

	if (gameEndAudio != null)
	{
		soundPlayer.Stream = gameEndAudio;
		soundPlayer.Play();
		GD.Print("🔊 Game end audio played");
		await ToSignal(soundPlayer, "finished");
	}
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
				GD.Print(tagID);
				GD.Print($"📟 SCANNED tagID: '{tagID}'");
				GD.Print($"🎯 EXPECTED currentTarget: '{player1TargetTag}'");

				if (!tagID.Trim().Equals(player1TargetTag.Trim()))
				{
					GD.Print("⚠ tagID != currentTarget");
					GD.Print($"tagID (length {tagID.Trim().Length}): '{tagID.Trim()}'");
					GD.Print($"currentTarget (length {player1TargetTag.Trim().Length}): '{player1TargetTag.Trim()}'");
				}
				else
				{
					GD.Print($"✅ Tag MATCH: {tagID}");
				}


				if (!tagID.StartsWith("0x")) {
					GD.Print("Error: Tag doesn't start with 0x");
					return;
				}
					
				
				if (tagID == lastProcessedUID && now - lastUIDTime < 2.0)
					return;

				GD.Print("tagID is being read I think");
				UIDLabel.Text = tagNameMap.ContainsKey(tagID) ? $"Scanned Tag: {tagNameMap[tagID]}" : "Unknown Tag";


				string currentTarget = player1TargetTag;
				GD.Print($"Comparing scanned tagID: '{tagID}' to currentTarget: '{currentTarget}'");
				bool isCorrect = tagID == currentTarget;
				if (!tagID.Trim().Equals(currentTarget.Trim()))
				{
					GD.Print("⚠ tagID != currentTarget");
					GD.Print($"tagID (length {tagID.Trim().Length}): '{tagID.Trim()}'");
					GD.Print($"currentTarget (length {currentTarget.Trim().Length}): '{currentTarget.Trim()}'");
				}
				else
				{
					GD.Print($"✅ Tag MATCH: {tagID}");
				}


				if (isCorrect)
				{
					text.Text = "Correct Tag! +1 Point";

					if (serialPort.IsOpen) serialPort.WriteLine("GREEN:8000");

					player1Score++;
					sfxPlayer.Stream = GD.Load<AudioStream>("res://assets/V3 Sounds/correct-156911.mp3");
					sfxPlayer.Play();

					PlayClue1AnswerLine(tagID, player1Stage);
					await ToSignal(soundPlayer, "finished");
					player1Stage++;
					GD.Print($"🎯 Advanced to stage {player1Stage}");

					// 🎧 Play stage start line if it exists
					if (stageStartAudioMap.TryGetValue(player1Stage, out var stageIntro))
					{
					if (serialPort.IsOpen)
					{
						serialPort.WriteLine("YELLOW:13000");
						GD.Print("🟡 Sent YELLOW command for stage intro");
					}

					soundPlayer.Stream = stageIntro;
					soundPlayer.Play();
					GD.Print($"🔊 Playing stage {player1Stage} start line");
					await ToSignal(soundPlayer, "finished");
				}


					if (player1Stage == 1) 
					{
						player1TargetTag = GetUniqueTag(bereavedUIDs);
						string tagName = tagNameMap[player1TargetTag];
						var (_, gender) = thirdClueAudioMap[tagName];
						player1Stage3TargetGender = gender;
					}
					else if (player1Stage == 2) 
					{
						player1TargetTag = GetUniqueTag(psychicUIDs);
					}
					else {
						DeclareWinner("Player 1");
					}

					UpdateScoreLabels();
					PlayClueForCurrentTag();
				}
				else
				{
					text.Text = "Wrong Tag";
					if (serialPort.IsOpen)
					{
						serialPort.WriteLine("RED:6000");
					}
					// ❌ Play error buzz
					sfxPlayer.Stream = GD.Load<AudioStream>("res://assets/V3 Sounds/wrong-100536.mp3");
					sfxPlayer.Play();

					string gender = GetGender(tagID);
					GD.Print($"👤 Gender for scanned tag: {gender}");

					AudioStream rejectionSound = player1Stage switch
					{
						0 => stage0RejectionMap.GetValueOrDefault("default"),
						1 => stage1RejectionMap.GetValueOrDefault(gender),
						2 => stage2RejectionMap.GetValueOrDefault("default"),
						_ => null
					};



					if (rejectionSound != null)
					{
						soundPlayer.Stream = rejectionSound;
						soundPlayer.Play();
						GD.Print($"🔊 Rejection sound played for stage {player1Stage}");
						await ToSignal(soundPlayer, "finished"); // ⏳ wait before playing the clue
					}
					else
					{
						GD.PrintErr($"❌ No rejection sound found for stage {player1Stage}");
					}
					// ✅ Resend clue color before replaying clue
					if (player1Stage == 1 && serialPort.IsOpen)
					{
						serialPort.WriteLine("YELLOW:13000");
						GD.Print("🟡 Sent YELLOW command before repeating stage 1 clue");
					}
					else if (player1Stage == 2 && serialPort.IsOpen)
					{
						serialPort.WriteLine("PURPLE:16000");
						GD.Print("🟣 Sent PURPLE command before repeating stage 2 clue");
					}
					// ✅ Now play the clue AFTER the rejection sound finishes
					PlayClueForCurrentTag();
				}
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
		{ "shoulderSweaterMan", GD.Load<AudioStream>("res://assets/V3 Sounds/ShoulderSweaterMan_Clue1.mp3") },
		{ "trumpHat", GD.Load<AudioStream>("res://assets/V3 Sounds/TrumpHat_Clue1.mp3") },
		{ "grandpaGrandson", GD.Load<AudioStream>("res://assets/V3 Sounds/GrandpaGrandson_Clue1.mp3") },
		{ "armyLady", GD.Load<AudioStream>("res://assets/V3 Sounds/ArmyLady_Clue1.mp3") },
		{ "oldCouple", GD.Load<AudioStream>("res://assets/V3 Sounds/OldCouple_Clue1.mp3") },
		{ "purpleDress", GD.Load<AudioStream>("res://assets/V3 Sounds/PurpleDress_Clue1.mp3") },
		{ "suitCaseMan", GD.Load<AudioStream>("res://assets/V3 Sounds/SuitCaseMan_Clue1.mp3") },
		{ "lilGirl", GD.Load<AudioStream>("res://assets/V3 Sounds/LilGirl_Clue1.mp3") }
	};
	
}

private void LoadSecondClueAudio()
{
	secondClueAudioMap = new Dictionary<string, AudioStream>
	{
		{ "shortShortBlonde", GD.Load<AudioStream>("res://assets/V3 Sounds/ThongIsland.mp3") },
		{ "redGrayLady", GD.Load<AudioStream>("res://assets/V3 Sounds/Agatha.mp3") },
		{ "whiteTux", GD.Load<AudioStream>("res://assets/V3 Sounds/Ronan.mp3") },
		{ "caneGrandpa", GD.Load<AudioStream>("res://assets/V3 Sounds/Anthorpe.mp3") },
		{ "handHipsLady", GD.Load<AudioStream>("res://assets/V3 Sounds/Clementine.mp3") },
		{ "blondeChild", GD.Load<AudioStream>("res://assets/V3 Sounds/Skittles.mp3") }
	};
}

private void LoadThirdClueAudio()
{
	thirdClueAudioMap = new Dictionary<string, (AudioStream, string)>
	{
		{ "blondeSuitCase", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Blonde.mp3"), "female") },
		{ "whiteShirtMan", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_WhiteShirt.mp3"), "male") },
		{ "blondeCouple", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Blonde.mp3"), "male") },
		{ "redPantsLady", (GD.Load<AudioStream>("res://assets/Upgraded Sound Assets/Ghosts_Clue3's/Clue3_RedPants.mp3"), "female") },
		{ "yellowDress", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_YellowDress.mp3"), "female") },
		{ "greenHoodie", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_GreenHoodie.mp3"), "male") },
		{ "businessPocket", (GD.Load<AudioStream>("res://assets/Upgraded Sound Assets/Ghosts_Clue3's/Clue3_Pockets.mp3"), "male") },
		{ "blondeBrownShirt", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Blonde.mp3"), "female") },
		{ "lilBoy", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Basketball.mp3"), "child") },
		{ "blondeRedShirt", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Blonde.mp3"), "male")},
		{ "yellowCoatLady", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_YellowCoat.mp3"), "female") },
		{ "yellowCamera", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Camera.mp3"), "male") },
		{ "baldBusiness", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_Bald.mp3"), "male") },
		{ "professorGuy", (GD.Load<AudioStream>("res://assets/V3 Sounds/Clue_GreenVest.mp3"), "male") }
	};
}

private void LoadSuccessAudio()
{
	successAudioMap = new Dictionary<string, AudioStream>
	{
		{ "shoulderSweaterMan", GD.Load<AudioStream>("res://assets/V3 Sounds/ShoulderSweaterMan_Success1.mp3") },
		{ "trumpHat", GD.Load<AudioStream>("res://assets/V3 Sounds/TrumpHat_Success1.mp3") },
		{ "grandpaGrandson", GD.Load<AudioStream>("res://assets/V3 Sounds/GrandpaGrandson_Success1.mp3") },
		{ "armyLady", GD.Load<AudioStream>("res://assets/V3 Sounds/ArmyLady_Success1.mp3") },
		{ "oldCouple", GD.Load<AudioStream>("res://assets/V3 Sounds/OldCouple_Success1.mp3") },
		{ "purpleDress", GD.Load<AudioStream>("res://assets/V3 Sounds/PurpleDress_Success1.mp3") },
		{ "suitCaseMan", GD.Load<AudioStream>("res://assets/V3 Sounds/SuitCaseMan_Success1.mp3") },
		{ "lilGirl", GD.Load<AudioStream>("res://assets/V3 Sounds/LilGirl_Success1.mp3") }
	};
}

private void LoadSecondSuccessAudio()
{
	secondSuccessAudioMap = new Dictionary<string, AudioStream>
	{
		{ "blondeSuitCase", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Female.mp3")) },
		{ "whiteShirtMan", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "blondeCouple", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "redPantsLady", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Female.mp3")) },
		{ "yellowDress", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Female.mp3")) },
		{ "greenHoodie", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "businessPocket", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "blondeBrownShirt", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Female.mp3")) },
		{ "lilBoy", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Child.mp3")) },
		{ "blondeRedShirt", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "yellowCoatLady", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Female.mp3")) },
		{ "yellowCamera", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "baldBusiness", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) },
		{ "professorGuy", (GD.Load<AudioStream>("res://assets/V3 Sounds/Bereaved_Male.mp3")) }
	};
}

private void LoadThirdSuccessAudio()
{
	thirdSuccessAudioMap = new Dictionary<string, Dictionary<string, AudioStream>>
	{
		{
			"shoulderSweaterMan", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/ShoulderSweaterMan_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/ShoulderSweaterMan_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/ShoulderSweaterMan_Child.mp3") }
			}
		},
		{
			"trumpHat", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/TrumpHat_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/TrumpHat_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/TrumpHat_Child.mp3") }
			}
		},
		{
			"grandpaGrandson", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/GrandpaGrandson_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/GrandpaGrandson_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/GrandpaGrandson_Child.mp3") }
			}
		},
		{
			"armyLady", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/ArmyLady_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/ArmyLady_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/ArmyLady_Child.mp3") }
			}
		},
		{
			"oldCouple", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/OldCouple_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/OldCouple_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/OldCouple_Child.mp3") }
			}
		},
		{
			"purpleDress", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/PurpleDress_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/PurpleDress_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/PurpleDress_Child.mp3") }
			}
		},
		{
			"suitCaseMan", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/SuitCaseMan_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/SuitCaseMan_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/SuitCaseMan_Child.mp3") }
			}
		},
		{
			"lilGirl", new Dictionary<string, AudioStream>
			{
				{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/LilGirl_Male.mp3") },
				{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/LilGirl_Female.mp3") },
				{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/LilGirl_Child.mp3") }
			}
		}
	};
}

private void PlayClue1AnswerLine(string tagID, int stage)
{
	if (!tagNameMap.TryGetValue(tagID, out var tagName)) return;

	AudioStream stream = null;
	
	if (stage == 2) // this is now the final stage
{
	string gender = player1Stage3TargetGender;
	string ghostName = stage1GhostNameP1;

	if (thirdSuccessAudioMap.TryGetValue(ghostName, out var genderMap) &&
		genderMap.TryGetValue(gender, out var finalLine))
	{
		stream = finalLine;
		GD.Print($"🔊 Playing final stage branching line for ghost: {ghostName}, based on gender: {gender}");
	}
	else
	{
		GD.PrintErr($"❌ No final success line for ghost {ghostName} and gender {gender}");
	}
}

	else
	{
		// Use your normal maps for stage 0 and 1
		stream = stage switch
		{
		0 => successAudioMap.GetValueOrDefault(tagName),
		1 => secondSuccessAudioMap.GetValueOrDefault(tagName),
		_ => null
		};
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
	string tagID = player1TargetTag;

	if (!tagNameMap.TryGetValue(tagID, out var tagName)) return;

	int stage = player1Stage;

	AudioStream stream = null;

		if (stage == 1)
		{
			if (thirdClueAudioMap.TryGetValue(tagName, out var clueTuple))
			{
				stream = clueTuple.clue;
			}
		}
		else if (stage == 2)
		{
			stream = secondClueAudioMap.GetValueOrDefault(tagName);

			if (serialPort.IsOpen)
			{
				serialPort.WriteLine("PURPLE:10000");
				GD.Print("🟣 Sent PURPLE command for stage 2 clue");
			}
		}

	else
	{
		stream = stage switch
		{
			0 => clueAudioMap.GetValueOrDefault(tagName),
			2 => secondClueAudioMap.GetValueOrDefault(tagName),
			_ => null
		};
	}
		// 💙 Turn blue when stage 0 success line plays
			if (stage == 0 && serialPort.IsOpen)
			{
				serialPort.WriteLine("BLUE:13000");
				GD.Print("🔵 Sent BLUE command for stage 0 success line");
			}
		
	if (stream != null)
	{
		// 💙 Turn NeoPixel ring blue for stage 0 clue
		if (stage == 0 && serialPort.IsOpen)
		{
			serialPort.WriteLine("BLUE:8000");
			GD.Print("🔵 Sent BLUE command for stage 0 clue");
		}

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
private void LoadRejectionAudio()
{
	stage0RejectionMap = new Dictionary<string, AudioStream>
	{
		{ "default", GD.Load<AudioStream>("res://assets/V3 Sounds/Rejection_Stage1.mp3") }
	};

	stage1RejectionMap = new Dictionary<string, AudioStream>
	{
		{ "male", GD.Load<AudioStream>("res://assets/V3 Sounds/Rejection_Male.mp3") },
		{ "female", GD.Load<AudioStream>("res://assets/V3 Sounds/Rejection_Female.mp3") },
		{ "child", GD.Load<AudioStream>("res://assets/V3 Sounds/Rejection_Child.mp3") }
	};

	stage2RejectionMap = new Dictionary<string, AudioStream>
	{
		{ "default", GD.Load<AudioStream>("res://assets/V3 Sounds/Rejection_Stage2.mp3") }
	};
}
private string GetGender(string tagID)
{
	if (!tagNameMap.TryGetValue(tagID, out var name)) return "default";
	if (!characterGenderMap.TryGetValue(name, out var gender)) return "default";
	return gender;
}


private void LoadCharacterGenders()
{
	characterGenderMap = new Dictionary<string, string>
	{
		{ "shoulderSweaterMan", "male" },
		{ "trumpHat", "male" },
		{ "grandpaGrandson", "male" },
		{ "armyLady", "female" },
		{ "oldCouple", "female" },
		{ "purpleDress", "female" },
		{ "suitCaseMan", "male" },
		{ "lilGirl", "child" },
		{ "shortShortBlonde", "female" },
		{ "redGrayLady", "female" },
		{ "whiteTux", "male" },
		{ "caneGrandpa", "male" },
		{ "handHipsLady", "female" },
		{ "blondeChild", "child" },
		{ "blondeSuitCase", "female" },
		{ "whiteShirtMan", "male" },
		{ "blondeCouple", "male" },
		{ "redPantsLady", "female" },
		{ "yellowDress", "female" },
		{ "greenHoodie", "male" },
		{ "businessPocket", "male" },
		{ "blondeBrownShirt", "female" },
		{ "lilBoy", "child" },
		{ "blondeRedShirt", "male" },
		{ "yellowCoatLady", "female" },
		{ "yellowCamera", "male" },
		{ "baldBusiness", "male" },
		{ "professorGuy", "male" }
	};
}
private void LoadStageIntroAudio()
{
	gameStartAudio = GD.Load<AudioStream>("res://assets/V3 Sounds/Boss_Intro.mp3");
	gameEndAudio = GD.Load<AudioStream>("res://assets/V3 Sounds/Boss_End.mp3");

	stageStartAudioMap = new Dictionary<int, AudioStream>
	{
		{ 0, GD.Load<AudioStream>("res://assets/V3 Sounds/BossStage1.mp3") },
		{ 1, GD.Load<AudioStream>("res://assets/V3 Sounds/BossStage2.mp3") },
		{ 2, GD.Load<AudioStream>("res://assets/V3 Sounds/BossStage3.mp3") }
	};
}

}
