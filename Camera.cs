namespace Asciifactory;

/// <summary>
/// Camera that determines which part of the world is visible.
/// Follows the player and provides world-to-screen coordinate conversion.
/// </summary>
public class Camera
{
    public int ViewportWidth { get; }
    public int ViewportHeight { get; }
    
    /// <summary>
    /// Top-left world coordinate of the viewport.
    /// </summary>
    public int WorldOffsetX { get; private set; }
    public int WorldOffsetY { get; private set; }
    
    public Camera(int viewportWidth, int viewportHeight)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }
    
    /// <summary>
    /// Centers the camera on a world position. Reserves bottom rows for HUD.
    /// </summary>
    public void CenterOn(int worldX, int worldY, int hudHeight = 3)
    {
        WorldOffsetX = worldX - ViewportWidth / 2;
        WorldOffsetY = worldY - (ViewportHeight - hudHeight) / 2;
    }
    
    /// <summary>
    /// Converts world coordinates to screen (viewport) coordinates.
    /// Returns null if the position is outside the viewport.
    /// </summary>
    public (int ScreenX, int ScreenY)? WorldToScreen(int worldX, int worldY)
    {
        int sx = worldX - WorldOffsetX;
        int sy = worldY - WorldOffsetY;
        
        if (sx < 0 || sx >= ViewportWidth || sy < 0 || sy >= ViewportHeight)
            return null;
        
        return (sx, sy);
    }
    
    /// <summary>
    /// Converts screen (viewport) coordinates to world coordinates.
    /// </summary>
    public (int WorldX, int WorldY) ScreenToWorld(int screenX, int screenY)
    {
        return (screenX + WorldOffsetX, screenY + WorldOffsetY);
    }
    
    /// <summary>
    /// Returns the world coordinates of the top-left corner of the viewport.
    /// </summary>
    public (int X, int Y) GetTopLeft() => (WorldOffsetX, WorldOffsetY);
}