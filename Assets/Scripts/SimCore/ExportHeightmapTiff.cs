using UnityEngine;
using System.IO;

public class ExportHeightmapTiff : MonoBehaviour
{
    [ContextMenu("Export Heightmap to TIFF")]
    public void Export()
    {
        Terrain terrain = GetComponent<Terrain>();
        TerrainData terrainData = terrain.terrainData;

        if (terrainData == null)
        {
            Debug.LogError("No TerrainData found!");
            return;
        }

        int res = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, res, res);

        byte[] tiff = EncodeTiff16(heights, res, res);
        string path = "Assets/heightmap.tiff";
        File.WriteAllBytes(path, tiff);

        Debug.Log($"Heightmap exported to {path} ({res}x{res}, 16-bit grayscale TIFF)");
    }

    private static byte[] EncodeTiff16(float[,] heights, int width, int height)
    {
        int numEntries    = 9;
        int ifdOffset     = 8;
        int ifdSize       = 2 + numEntries * 12 + 4;
        int imageOffset   = ifdOffset + ifdSize;
        int imageByteSize = width * height * 2;

        byte[] buf = new byte[imageOffset + imageByteSize];
        int pos = 0;

        // TIFF header (little-endian)
        buf[pos++] = 0x49; buf[pos++] = 0x49; // "II"
        buf[pos++] = 42;   buf[pos++] = 0;     // magic
        WriteI32(buf, pos, ifdOffset); pos += 4;

        // IFD
        WriteI16(buf, pos, numEntries); pos += 2;

        WriteEntry(buf, ref pos, 256, 4, 1, width);         // ImageWidth
        WriteEntry(buf, ref pos, 257, 4, 1, height);        // ImageLength
        WriteEntry(buf, ref pos, 258, 3, 1, 16);            // BitsPerSample
        WriteEntry(buf, ref pos, 259, 3, 1, 1);             // Compression = none
        WriteEntry(buf, ref pos, 262, 3, 1, 1);             // PhotometricInterpretation = BlackIsZero
        WriteEntry(buf, ref pos, 273, 4, 1, imageOffset);   // StripOffsets
        WriteEntry(buf, ref pos, 278, 4, 1, height);        // RowsPerStrip
        WriteEntry(buf, ref pos, 279, 4, 1, imageByteSize); // StripByteCounts
        WriteEntry(buf, ref pos, 284, 3, 1, 1);             // PlanarConfiguration = chunky

        WriteI32(buf, pos, 0); pos += 4; // no next IFD

        // Pixel data — flip Y so row 0 = top of image (matches PNG export convention)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ushort v = (ushort)(heights[height - 1 - y, x] * 65535f);
                buf[pos++] = (byte)(v & 0xFF);
                buf[pos++] = (byte)(v >> 8);
            }
        }

        return buf;
    }

    private static void WriteI16(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteI32(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteEntry(byte[] buf, ref int pos, int tag, int type, int count, int value)
    {
        WriteI16(buf, pos, tag);   pos += 2;
        WriteI16(buf, pos, type);  pos += 2;
        WriteI32(buf, pos, count); pos += 4;
        WriteI32(buf, pos, value); pos += 4;
    }
}
