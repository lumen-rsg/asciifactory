using System.Text;
using Asciifactory;
using Asciifactory.Network;

Console.OutputEncoding = Encoding.UTF8;

// Parse --god CLI flag
bool godMode = args.Contains("--god");

// Main loop: show menu → play game → return to menu on "Quit to Menu"
while (true)
{
    // Show the animated main menu
    var menu = new MainMenu();
    var (result, settings) = menu.Run();

    // Apply CLI god mode flag
    if (godMode)
        settings.GodMode = true;

    switch (result)
    {
        case MenuResult.Singleplayer:
        {
            Console.Clear();
            var game = new Game(settings);
            game.Run();
            if (game.ReturnToMainMenu)
                continue; // Re-show main menu
            break;
        }
        
        case MenuResult.MultiplayerHost:
        {
            Console.Clear();
            var mpGame = new Game(settings);
            
            var mpConfig = settings.Multiplayer!;
            var server = mpConfig.ExistingServer ?? new NetServer(mpConfig.Port);
            if (mpConfig.ExistingServer == null)
            {
                server.Start();
                
                // Add all lobby players (only for fresh server)
                if (mpConfig.FinalLobby != null)
                {
                    foreach (var lp in mpConfig.FinalLobby.Players)
                    {
                        server.AddHostPlayer(new PlayerInfo
                        {
                            Nickname = lp.Nickname,
                            Color = lp.Color,
                            IsHost = lp.Index == 0,
                        });
                    }
                }
            }
            
            mpGame.SetupMultiplayerHost(server, mpConfig);
            mpGame.Run();
            break;
        }
        
        case MenuResult.MultiplayerJoin:
        {
            Console.Clear();
            var mpConfig = settings.Multiplayer!;
            var client = new NetClient(mpConfig.HostIP, mpConfig.Port);
            
            var clientInfo = new PlayerInfo
            {
                Nickname = mpConfig.Nickname,
                Color = mpConfig.Color,
                IsHost = false,
            };
            
            if (!client.Connect(clientInfo))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Failed to connect: {client.Error}");
                Console.ResetColor();
                Thread.Sleep(2000);
                break;
            }
            
            // Wait for game start from server
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Connected! Waiting for host to start the game...");
            Console.ResetColor();
            
            while (!client.GameStarted)
            {
                Thread.Sleep(100);
            }
            
            mpConfig.FinalLobby = client.LobbyState;
            
            var mpGame = new Game(settings);
            mpGame.SetupMultiplayerClient(client, mpConfig);
            mpGame.Run();
            break;
        }
        
        case MenuResult.Exit:
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Thanks for checking out ASCIIFACTORY!");
            Console.ResetColor();
            return; // Exit the program
    }
    
    break; // Exit loop for non-menu-return cases
}