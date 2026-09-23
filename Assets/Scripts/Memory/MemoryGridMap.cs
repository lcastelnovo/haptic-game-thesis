using System;

namespace HapticResearch.Memory
{
    // Aritmetica della griglia del memory. Classe pura, come MazeMap: la logica di gioco
    // chiede a LEI dove sta il dito, non ai collider (quelli servono solo a far sentire le
    // tessere al guanto). Cosi' si prova fuori da Unity (Tools/MemoryTest).
    //
    // Coordinate del PARTECIPANTE, origine al centro della griglia: +x verso la sua destra,
    // +z lontano da lui. Colonna 0 alla sua sinistra, riga 0 la piu' vicina.
    // Indice di tessera = riga * Columns + colonna.
    public sealed class MemoryGridMap
    {
        public const int Gap = -1;       // tavolo nudo fra due tessere
        public const int Outside = -2;   // fuori dalla griglia

        public int Columns { get; }
        public int Rows { get; }
        public float TileSize { get; }
        public float TileGap { get; }

        public int TileCount => Columns * Rows;
        public float Width => Columns * TileSize + (Columns - 1) * TileGap;
        public float Depth => Rows * TileSize + (Rows - 1) * TileGap;

        private float Pitch => TileSize + TileGap;

        public MemoryGridMap(int columns, int rows, float tileSize, float tileGap)
        {
            if (columns <= 0 || rows <= 0) throw new ArgumentException("la griglia deve avere almeno una colonna e una riga");
            if (tileSize <= 0f || tileGap < 0f) throw new ArgumentException("misure di tessera non valide");
            Columns = columns;
            Rows = rows;
            TileSize = tileSize;
            TileGap = tileGap;
        }

        public int Index(int column, int row) => row * Columns + column;
        public int ColumnOf(int tile) => tile % Columns;
        public int RowOf(int tile) => tile / Columns;

        public void Center(int tile, out float x, out float z)
        {
            x = -Width * 0.5f + TileSize * 0.5f + ColumnOf(tile) * Pitch;
            z = -Depth * 0.5f + TileSize * 0.5f + RowOf(tile) * Pitch;
        }

        // In O(1): niente cicli sulle tessere, e' chiamata a ogni frame.
        public int Locate(float x, float z)
        {
            float u = x + Width * 0.5f;
            float v = z + Depth * 0.5f;
            if (u < 0f || v < 0f || u > Width || v > Depth) return Outside;

            int column = Math.Min((int)(u / Pitch), Columns - 1);
            int row = Math.Min((int)(v / Pitch), Rows - 1);
            if (u - column * Pitch > TileSize || v - row * Pitch > TileSize) return Gap;
            return Index(column, row);
        }
    }
}
