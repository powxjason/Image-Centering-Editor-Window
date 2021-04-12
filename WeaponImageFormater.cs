using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
///  This script takes a source image with a Locater Pixel in it, the color of that Locater Pixel, and what color that Locater Pixel should be
///  and outputs a formated version of that image with the position of the locater pixel in the center(top right center pixel, if the image result is even).
///  written by Powxjason. Discord: JimmyJohn's#7242
/// </summary>  
/// <remarks>
///  The BadCopier and PixelCleaner functions are really inefficient, if optimization is your goal here, start there.
/// </remarks>

public class WeaponImageFormater : EditorWindow {

    //storing the new files save location.
    string pathOfConvertedFile;

    // storing the source texture.
    Object imageToConvert;

    // storing the Locater Color and Desired Color
    Color locaterColor = new Color(1, 0, 0, 1);
    Color desiredColor = new Color(1, 1, 1, 1);

    // Where in unity this tool shows up (Don't change this unless you know what you're doing, or change it anyway, i'm not your mom).
    [MenuItem("Window/PowTool/Weapon Image Formater")]
    public static void ShowWindow()
    {
        GetWindow<WeaponImageFormater>("Weapon Image Formater");
    }

    void OnGUI()
    {
        // Credits to Me!
        GUILayout.Label("Created by Powxjason. Discord: JimmyJohn's#7242", EditorStyles.boldLabel);

        // A help box and field for the image.
        EditorGUILayout.HelpBox("Place an image in here to Convert it for use as a custom weapon. Images with compresion will usually fail, as the Locater pixel gets mixed in with the surrounding colors.", MessageType.Info);
        imageToConvert = EditorGUILayout.ObjectField("Image to Convert", imageToConvert, typeof(Texture2D), false);

        // A button that opens a window to select the output location
        if(GUILayout.Button("Set File Path"))
        {
            pathOfConvertedFile = EditorUtility.SaveFilePanel("Save new texture as PNG", "", ".png", "png");
        }


        // A help box and two color fields for the Locater Color and the Desired Color of the Locater Color in the new texture.
        EditorGUILayout.HelpBox("Set Locater Color to the color of the pixel. Set Desired Color to the color that pixel should be in the final image.", MessageType.Info);
        locaterColor = EditorGUILayout.ColorField("Locater Color", locaterColor);
        desiredColor = EditorGUILayout.ColorField("Desired Color", desiredColor);

        // A button that starts making the new texture.
        // EditorGUILayout.HelpBox("To see your new texture file, Right Click on the project window and select 'Refresh' or press CTRL + R to refresh the project window", MessageType.Info);
        if (GUILayout.Button("Convert Image"))
        {
            if (ConvertButton((Texture2D)imageToConvert, locaterColor, desiredColor, pathOfConvertedFile))
            {
                Debug.Log("Item Converted!");
            }
            else
            {
                Debug.Log("Converion was Canceled, or failed in an unexpected way");
            }
        }
    }

    // The main function of this script
    private bool ConvertButton(Texture2D texture, Color location, Color replace, string path)
    {
        // makes sure there is a texture selected.
        if (!texture)
        {
            Debug.LogError("No Image selected!");
            return false;
        }

        // makes sure a file path is selected
        if(path == null)
        {
            Debug.Log("No file path selected!");
        }

        // Checks every pixel in the image against the Locater Color.
        Vector2 pixels = PixelLocater(texture, location);

        // Errors if no pixels match the Locater Color.
        if (pixels.x == -1)
        {
            Debug.LogError("Locater Pixel not found! Check the alpha value of the locater color. Make sure image compression is disabled.");
            return false;
        }

        // Errors is more than one pixel matches the Locater Color.
        else if (pixels.x == -2)
        {
            Debug.LogError("More than 1 Locater pixel found! Try disabling compression in the Texture settings.");
            return false;
        }

        // variables for the original textures width and height.
        int textWidth = texture.width;
        int textHeight = texture.height;

        // makes sure the image will end up in the right corner after translation.
        Vector2 quadrant = QuadrantDetector(pixels, textWidth, textHeight);

        // variables that decompress the Vector 2 into two ints. (PixelLocater always returns ints placed into a Vector2).
        int pixelPosX = (int)pixels.x;
        int pixelPosY = (int)pixels.y;
        
        // Creates a version of the inital texture with the Locater Pixel swapped to the Desired Color
        Texture2D cleanedTexture = new Texture2D(texture.width, texture.height);
        cleanedTexture = BadCopier(texture, cleanedTexture, 0, 0);
        cleanedTexture.SetPixel(pixelPosX, pixelPosY, replace);

        // if statements that calculate the width of the texture based on where the Locater Pixel is
        int newTextureWidth = newTextureSizer((int)quadrant.x, textWidth, pixelPosX);
        int newTextureHeight = newTextureSizer((int)quadrant.y, textHeight, pixelPosY);

        // A new texture is created that will fit the Locater pixels position in the middle after translation.
        Texture2D newTexture = new Texture2D(newTextureWidth, newTextureHeight);

        // New textures in unity start with a transparent gray in all pixels, this sets them to fully transparent.
        newTexture = PixelCleaner(newTexture);

        // Copies the inital texture into the approprite position in the new texture.
        newTexture = BadCopier(cleanedTexture, newTexture, 
            CopyIndexer((int)quadrant.x, textWidth, pixelPosX),
            CopyIndexer((int)quadrant.y, textHeight, pixelPosY));
        
        // Encodes newTexture into PNG and saves it.
        byte[] bytes = newTexture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        AssetDatabase.Refresh();

        TextureImpOut(path);

        AssetDatabase.Refresh();

        return true;
    }

    // Copies every pixel in the source texture into the correct position in the newTexture and returns it.
    private Texture2D BadCopier(Texture2D originalText, Texture2D newText, int copyXPos, int copyYPos) 
    {
        Texture2D localText = newText;

        for(int x = 0; x < originalText.width; x++)
        {
            for(int y = 0; y < originalText.height; y++)
            {
                Color pickedColor = originalText.GetPixel(x, y);
                newText.SetPixel(x + copyXPos, y + copyYPos, pickedColor);
            }
        }

        return newText;
    }

    // Checks every pixel in the texture for a specific color.
    // returns (-1, -1) if the pixel isn't found and (-2, -2) if there is more than 1 pixel of that color.
    private Vector2 PixelLocater(Texture2D originalText, Color wantedColor)
    {
        Vector2 PixelStorage = new Vector2(-1, -1);

        for (int x = 0; x < originalText.width; x++)
        {
            for (int y = 0; y < originalText.height; y++)
            {
                if (originalText.GetPixel(x,y) == wantedColor)
                {              
                    if(PixelStorage == new Vector2(-1, -1))
                    {
                        PixelStorage = new Vector2(x, y);
                    }
                    else
                    {
                        PixelStorage = new Vector2(-2, -2);
                    }
                }
            }
        }

        return PixelStorage;
    }

    // Goes through every pixel in the texture and sets it to a completely transparent pixel. {color(0,0,0,0)}.
    private Texture2D PixelCleaner(Texture2D text)
    {
        Texture2D newText = text;
        
        for (int x = 0; x < newText.width; x++)
        {
            for (int y = 0; y < newText.height; y++)
            {
                newText.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }

        return newText;
    }

    // returns which of the four quadrants of the source texture the Locator Pixel is in.
    private Vector2 QuadrantDetector(Vector2 pos, int width, int height)
    {
        Vector2 quadrant = new Vector2(1,1);

        if(pos.x < (width / 2))
        {
            quadrant.x = 0;
        }
        
        if(pos.y < (height / 2))
        {
            quadrant.y = 0;
        }
       
        return quadrant;
    }

    // NOTE: newTextureSizer and CopyIndexer will aim for opposite quadrants
    // newTextureSizer aims with the Locater pixel quadrant, CopyIndexer aims opposite of the Locater pixel quadrant.

    // uses the position of the Locater Pixel and the size of the orignial texture to calculate the size for the new canvas.
    private int newTextureSizer(int quadrant, int textSize, int pixelSize)
    {       
        if(quadrant == 1)
        {
            return (textSize) - (textSize - (pixelSize * 2));
        }
        else
        {
            return (textSize * 2) - (pixelSize * 2);
        }
    }

    // uses the same inputs of newTextureSizer to find the position to copy the original texture in the new canvas.
    private int CopyIndexer(int quadrant, int textSize, int pixelSize)
    {
        if (quadrant == 0)
        {
            return (textSize - (pixelSize * 2));
        }
        else
        {
            return (pixelSize * 2);
        }
    }

    // removes compression, sets the new texture to readable from scripts, and sets the type to sprite.
    private void TextureImpOut(string path)
    {
        string shortPath = path + ".meta";

        string[][] list = new string[5][];
        list[0] = new string[] { "alphaIsTransparency: 0", "alphaIsTransparency: 1" };
        list[1] = new string[] { "isReadable: 0", "isReadable: 1" };
        list[2] = new string[] { "textureCompression: 1", "textureCompression: 0" };
        list[3] = new string[] { "filterMode: -1", "filterMode: 0" };
        list[4] = new string[] { "textureType: 0", "textureType: 8" };

        for(int i = 0; i < list.Length - 1; i++)
        {
            File.WriteAllText(shortPath, Regex.Replace(File.ReadAllText(shortPath), list[i][0], list[i][1]));
        }
    }

}
