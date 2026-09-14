using UnityEngine;

// Wrapper class to handle Vector2 serialization
public class Vector2Wrapper
{
    public float x;
    public float y;

    public Vector2Wrapper(Vector2 vector)
    {
        this.x = vector.x;
        this.y = vector.y;
    }

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
}