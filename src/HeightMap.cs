using Godot;

public partial class HeightMap : Node2D
{
    public override void _Ready()
    {
        GenerateImage();
    }
    
    void GenerateImage()
    {
        int width = 1920, height = 1080;
        var img = Image.Create(width, height, false, Image.Format.L8);
        float hY = 0.5f, stepY = 0f;
        var rng = new RandomNumberGenerator();
        var savedState = rng.State;
        for (int y = 0; y < height; y++)
        {
            hY = Mathf.Clamp(hY + stepY, 0f, 1f);
            if (GD.RandRange(0, 30) == 0)
            {
                stepY = (Mathf.IsZeroApprox(stepY) ? (float)GD.RandRange(-0.01f, 0.01f) : 0f);
            }
            
            float hX = 0.5f, stepX = 0f;
            rng.State = savedState;
            for (int x = 0; x < width; x++)
            {
                hX = Mathf.Clamp(hX + stepX, 0f, 1f);
                if (rng.RandiRange(0, 30) == 0)
                {
                    stepX = (Mathf.IsZeroApprox(stepX) ? (float)rng.RandfRange(-0.01f, 0.01f) : 0f);
                }

                float h = (hX + hY) / 2f;
                img.SetPixel(x, y, new Color(h, h, h));
            }
        }
        img.SavePng("height_map.png");
    }
}