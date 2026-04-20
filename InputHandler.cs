namespace Asciifactory;

/// <summary>
/// Input commands that the game can process.
/// </summary>
public enum InputCommand
{
    None,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Mine,
    Build,
    Inventory,
    Craft,
    Rotate,
    Interact,
    Quit,
    Help,
    Calculator,
    Scan,
    GodMode,
    NextType,
    PrevType,
    TakeItem,
    Enter,
}

/// <summary>
/// Handles non-blocking keyboard input from the terminal.
/// Maps keys to game commands.
/// </summary>
public class InputHandler
{
    private ConsoleKey _lastKey;
    private char _lastKeyChar;
    
    /// <summary>The character of the last key that produced a command.</summary>
    public char LastKeyChar => _lastKeyChar;
    
    /// <summary>
    /// Polls for keyboard input without blocking.
    /// Drains ALL buffered keys (from key repeat) and returns only the last one.
    /// This prevents key-buffer pileups that cause lag/crashes.
    /// </summary>
    public InputCommand Poll()
    {
        if (!Console.KeyAvailable)
            return InputCommand.None;
        
        // Drain all buffered keys, keep only the last meaningful one
        InputCommand lastCommand = InputCommand.None;
        char lastChar = '\0';
        while (Console.KeyAvailable)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            _lastKey = keyInfo.Key;
            lastChar = keyInfo.KeyChar; // Always track character for digit keys
            var cmd = MapKey(keyInfo);
            if (cmd != InputCommand.None)
                lastCommand = cmd;
        }
        _lastKeyChar = lastChar;
        
        return lastCommand;
    }
    
    /// <summary>
    /// Waits for and returns the next key press (blocking).
    /// </summary>
    public InputCommand WaitForKey()
    {
        var keyInfo = Console.ReadKey(intercept: true);
        return MapKey(keyInfo);
    }
    
    private static InputCommand MapKey(ConsoleKeyInfo keyInfo)
    {
        // Check character first for keys that vary by keyboard layout (e.g., / on macOS)
        if (keyInfo.KeyChar == '/' || keyInfo.KeyChar == '?')
            return InputCommand.NextType;
        
        return keyInfo.Key switch
        {
            ConsoleKey.W or ConsoleKey.UpArrow => InputCommand.MoveUp,
            ConsoleKey.S or ConsoleKey.DownArrow => InputCommand.MoveDown,
            ConsoleKey.A or ConsoleKey.LeftArrow => InputCommand.MoveLeft,
            ConsoleKey.D or ConsoleKey.RightArrow => InputCommand.MoveRight,
            ConsoleKey.Spacebar => InputCommand.Mine,
            ConsoleKey.B => InputCommand.Build,
            ConsoleKey.I => InputCommand.Inventory,
            ConsoleKey.C => InputCommand.Craft,
            ConsoleKey.R => InputCommand.Rotate,
            ConsoleKey.E => InputCommand.Interact,
            ConsoleKey.T => InputCommand.TakeItem,
            ConsoleKey.P => InputCommand.Calculator,
            ConsoleKey.Escape or ConsoleKey.Q => InputCommand.Quit,
            ConsoleKey.H or ConsoleKey.F1 => InputCommand.Help,
            ConsoleKey.F => InputCommand.Scan,
            ConsoleKey.D5 => InputCommand.GodMode,
            ConsoleKey.Tab => (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0 
                ? InputCommand.PrevType : InputCommand.NextType,
            ConsoleKey.Oem4 => InputCommand.PrevType,     // [ key
            ConsoleKey.Oem6 => InputCommand.NextType,     // ] key
            ConsoleKey.Oem2 => InputCommand.NextType,     // / key
            ConsoleKey.Enter => InputCommand.Enter,
            _ => InputCommand.None
        };
    }
}