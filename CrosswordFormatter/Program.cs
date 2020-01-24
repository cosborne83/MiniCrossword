using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CrosswordFormatter
{
    internal class Program
    {
        private static readonly List<Entry> Entries = new List<Entry>();
        private static int[,] _cells;

        private static void Main(string[] args)
        {
            var template = new[]
            {
                "**dab",
                "*wore",
                "saute",
                "eggs*",
                "ash**"
            };

            var height = template.Length;
            if (height == 0) return;
            var width = template[0].Length;
            if (width == 0) return;

            _cells = new int[width, height];
            var grid = new char[width, height];
            for (var y = 0; y < height; y++)
            {
                var row = template[y].ToLowerInvariant();
                if (row.Length != width) throw new InvalidOperationException("All rows must have the same length");
                for (var x = 0; x < row.Length; x++)
                {
                    var c = row[x];
                    if (c == '*')
                    {
                        _cells[x, y] = -1;
                        grid[x, y] = c;
                        continue;
                    }

                    if (c == '.' || c >= 'a' && c <= 'z')
                    {
                        grid[x, y] = c;
                        continue;
                    }

                    throw new InvalidOperationException("Invalid input");
                }
            }

            for (var y = 0; y < height; y++)
            {
                var word = string.Empty;
                for (var x = 0; x < width; x++)
                {
                    word = HasSpaces(x, y, _cells[x, y] == -1, word, false) ? string.Empty : word + grid[x, y];
                }

                AddEntry(width, y, word, false);
            }

            for (var x = 0; x < width; x++)
            {
                var word = string.Empty;
                for (var y = 0; y < height; y++)
                {
                    word = HasSpaces(x, y, _cells[x, y] == -1, word, true) ? string.Empty : word + grid[x, y];
                }

                AddEntry(x, height, word, true);
            }

            var numbered = Entries
                .OrderBy(e => e.Y)
                .ThenBy(e => e.X)
                .GroupBy(e => new { e.X, e.Y })
                .Select((g, i) => new { Number = i + 1, Entries = g });

            var across = new SortedDictionary<int, string>();
            var down = new SortedDictionary<int, string>();

            foreach (var item in numbered)
            {
                foreach (var entry in item.Entries)
                {
                    _cells[entry.X, entry.Y] = item.Number;
                    var dict = entry.Vertical ? down : across;
                    dict.Add(item.Number, entry.Word);
                }
            }

            using (var writer = new StreamWriter("crossword.html"))
            {
                writer.WriteLine(@"<html>
<head>
<title>Crossword</title>
<style>
body
{
  font-family: Calibri;
}
table
{
  border-collapse: collapse;
}
td
{
  vertical-align: top;
}
td.clues
{
  padding: 0px 25px;
}
td.clues td
{
  width: 350px;
  font-size: 16pt;
}
table.grid td
{
  border: 2px solid #888;
  width: 96px;
  height: 108px;
  margin: 0;
  padding: 1px 5px;
  font-size: 16pt;
  font-weight: bold;
}
table.grid td.filled
{
  background-color: #CCC;
}
</style>
</head>
<body>
<h1>Name here</h1>
<table>
<tr>
<td>
<table class=""grid"">");

                for (var y = 0; y < height; y++)
                {
                    writer.WriteLine("<tr>");
                    for (var x = 0; x < width; x++)
                    {
                        var cell = _cells[x, y];
                        switch (cell)
                        {
                            case -1:
                                writer.WriteLine("<td class=\"filled\"></td>");
                                break;
                            case 0:
                                writer.WriteLine("<td></td>");
                                break;
                            default:
                                writer.WriteLine($"<td>{cell}</td>");
                                break;
                        }
                    }
                    writer.WriteLine("</tr>");
                }

                writer.WriteLine(@"</table>
</td>
<td class=""clues"">
<table>
<tr>
<td>
<h2>Across</h2>
<ol>");
                foreach (var pair in across)
                {
                    writer.WriteLine($"<li value=\"{pair.Key}\">{pair.Value}</li>");
                }

                writer.WriteLine(@"</ol>
</td>
<td>
<h2>Down</h2>
<ol>");
                foreach (var pair in down)
                {
                    writer.WriteLine($"<li value=\"{pair.Key}\">{pair.Value}</li>");
                }

                writer.WriteLine(@"</td>
</tr>
</table>
</td>
</tr>
</table>
</body>
</html>");
            }
        }

        private static bool HasSpaces(int x, int y, bool filled, string word, bool vertical)
        {
            if (!filled) return false;
            AddEntry(x, y, word, vertical);
            return true;
        }

        private static void AddEntry(int x, int y, string word, bool vertical)
        {
            var length = word.Length;
            if (length < 2) return;
            var startX = vertical ? x : x - length;
            var startY = vertical ? y - length : y;
            Entries.Add(new Entry(startX, startY, word, vertical));
        }

        private class Entry
        {
            public readonly int X;
            public readonly int Y;
            public readonly string Word;
            public readonly bool Vertical;

            public Entry(int x, int y, string word, bool vertical)
            {
                X = x;
                Y = y;
                Word = word;
                Vertical = vertical;
            }
        }
    }
}
