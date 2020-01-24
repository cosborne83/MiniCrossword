using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Crossword
{
    internal class Program
    {
        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Dictionary<int, List<string>> WordsByLength = new Dictionary<int, List<string>>();
        private static readonly HashSet<string> AllWords = new HashSet<string>();
        private static char[,] _grid;
        private static int _width;
        private static int _height;

        private static void Main(string[] args)
        {
            var template = new[]
            {
                "s..e.",
                ".*...",
                ".l.r.",
                "...*.",
                ".b..s"
            };

            _height = template.Length;
            if (_height == 0) return;
            _width = template[0].Length;
            if (_width == 0) return;

            _grid = new char[_width, _height];
            for (var y = 0; y < _height; y++)
            {
                var row = template[y].ToLowerInvariant();
                if (row.Length != _width) throw new InvalidOperationException("All rows must have the same length");
                for (var x = 0; x < row.Length; x++)
                {
                    var c = row[x];
                    if (c == '*' || c == '.' || c >= 'a' && c <= 'z')
                    {
                        _grid[x, y] = c;
                        continue;
                    }

                    throw new InvalidOperationException("Invalid input");
                }
            }

            for (var y = 0; y < _height; y++)
            {
                var length = 0;
                for (var x = 0; x < _width; x++)
                {
                    length = HasSpaces(x, y, _grid[x, y], length, false) ? 0 : length + 1;
                }

                AddEntry(_width, y, length, false);
            }

            for (var x = 0; x < _width; x++)
            {
                var length = 0;
                for (var y = 0; y < _height; y++)
                {
                    length = HasSpaces(x, y, _grid[x, y], length, true) ? 0 : length + 1;
                }

                AddEntry(x, _height, length, true);
            }

            var lengths = new SortedSet<int>();
            var horzEntryGrid = new Entry[_width, _height];
            var vertEntryGrid = new Entry[_width, _height];
            foreach (var entry in Entries)
            {
                if (entry.NumSpaces > 0) lengths.Add(entry.Length);
                for (var i = 0; i < entry.Length; i++)
                {
                    entry.GetCoords(i, out var x, out var y);
                    var entryGrid = entry.Vertical ? vertEntryGrid : horzEntryGrid;
                    if (entryGrid[x, y] != null) throw new InvalidOperationException("Grid already has an entry");
                    entryGrid[x, y] = entry;
                }
            }

            foreach (var entry in Entries)
            {
                if (entry.Vertical) continue;
                var intersectGrid = vertEntryGrid;
                for (var i = 0; i < entry.Length; i++)
                {
                    entry.GetCoords(i, out var x, out var y);
                    var intersectEntry = intersectGrid[x, y];
                    if (intersectEntry == null) continue;

                    entry.Intersections[i] = intersectEntry.CreateIntersection(x, y, entry);
                }
            }

            var numbered = Entries
                .OrderBy(e => e.Y)
                .ThenBy(e => e.X)
                .GroupBy(e => new { e.X, e.Y })
                .Select((g, i) => new { Number = i + 1, Entries = g});

            foreach (var item in numbered)
            {
                foreach (var entry in item.Entries)
                {
                    entry.Index = item.Number;
                }
            }
            
            Console.WriteLine("Need words of lengths:");
            foreach (var length in lengths)
            {
                WordsByLength.Add(length, new List<string>());
                Console.WriteLine(length);
            }

            using (var reader = new StreamReader("words58k.txt"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!WordsByLength.TryGetValue(line.Length, out var words)) continue;
                    words.Add(line);
                    AllWords.Add(line);
                }
            }

            Build(0);
         }

        private static void Build(int entryIndex)
        {
            if (entryIndex == Entries.Count)
            {
                PrintGrid();
                return;
            }

            var entry = Entries[entryIndex];
            if (entry.IsComplete)
            {
                if (!entry.Verify(AllWords)) return;
                Build(entryIndex + 1);
                return;
            }

            foreach (var word in FindWords(entry))
            {
                ApplyLetter(entry, entryIndex, word, 0);
            }
        }

        private static void ApplyLetter(Entry entry, int entryIndex, string word, int charIndex)
        {
            if (charIndex == word.Length)
            {
                Entry firstIncompleteIntersector = null;
                foreach (var intersector in entry.Intersections)
                {
                    if (intersector == null) continue;
                    if (intersector.IsComplete)
                    {
                        if (!intersector.Verify(AllWords)) return;
                        continue;
                    }

                    if (!FindWords(intersector).Any()) return;
                    if (firstIncompleteIntersector != null) continue;
                    firstIncompleteIntersector = intersector;
                }

                if (firstIncompleteIntersector == null)
                {
                    Build(entryIndex + 1);
                }
                else
                {
                    foreach (var intersectWord in FindWords(firstIncompleteIntersector))
                    {
                        ApplyLetter(firstIncompleteIntersector, entryIndex, intersectWord, 0);
                    }
                }

                return;
            }

            var needsRevert = entry.ApplyLetter(word, charIndex);
            ApplyLetter(entry, entryIndex, word, charIndex + 1);
            if (needsRevert) entry.RevertLetter(charIndex);
        }

        private static void PrintGrid()
        {
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    Console.Write(_grid[x, y]);
                }

                Console.WriteLine();
            }

            Console.WriteLine();

            Console.WriteLine("Across:");
            foreach (var item in Entries.Where(e => !e.Vertical).OrderBy(e => e.Index)) Console.WriteLine($"{item.Index}: {item.GetWord()}");
            Console.WriteLine();
            Console.WriteLine("Down:");
            foreach (var item in Entries.Where(e => e.Vertical).OrderBy(e => e.Index)) Console.WriteLine($"{item.Index}: {item.GetWord()}");
            Console.WriteLine();
            Console.WriteLine();
        }

        private static IEnumerable<string> FindWords(Entry entry)
        {
            foreach (var word in WordsByLength[entry.Length])
            {
                if (!entry.Matches(word)) continue;
                yield return word;
            }
        }

        private static bool HasSpaces(int x, int y, char c, int length, bool vertical)
        {
            if (c == '*')
            {
                AddEntry(x, y, length, vertical);
                return true;
            }

            return false;
        }

        private static void AddEntry(int x, int y, int length, bool vertical)
        {
            if (length < 2) return;
            var startX = vertical ? x : x - length;
            var startY = vertical ? y - length : y;
            Entries.Add(new Entry(_grid, startX, startY, length, vertical));
        }

        private class Entry
        {
            private readonly char[,] _grid;
            public readonly int X;
            public readonly int Y;
            public readonly int Length;
            public readonly bool Vertical;
            public int NumSpaces;
            public readonly Entry[] Intersections;
            private bool _isVerified;

            public bool IsComplete => NumSpaces == 0;
            public int Index { get; set; }

            public Entry(char[,] grid, int x, int y, int length, bool vertical)
            {
                _grid = grid;
                X = x;
                Y = y;
                Length = length;
                Vertical = vertical;
                Intersections = new Entry[Length];
                foreach (var c in GetLetters())
                {
                    if (c != '.') continue;
                    NumSpaces++;
                }

                _isVerified = NumSpaces == 0;
            }

            public void GetCoords(int index, out int x, out int y)
            {
                x = Vertical ? X : X + index;
                y = Vertical ? Y + index : Y;
            }

            public Entry CreateIntersection(int x, int y, Entry entry)
            {
                int position;
                if (Vertical)
                {
                    if (x != X) throw new InvalidOperationException("Invalid intersection");
                    position = y - Y;
                }
                else
                {
                    if (y != Y) throw new InvalidOperationException("Invalid intersection");
                    position = x - X;
                }

                Intersections[position] = entry;
                return this;
            }

            private IEnumerable<char> GetLetters()
            {
                if (Vertical)
                {
                    for (var i = 0; i < Length; i++)
                    {
                        yield return _grid[X, Y + i];
                    }
                }
                else
                {
                    for (var i = 0; i < Length; i++)
                    {
                        yield return _grid[X + i, Y];
                    }
                }
            }

            public bool Matches(string word)
            {
                if (Vertical)
                {
                    for (var i = 0; i < Length; i++)
                    {
                        var p = _grid[X, Y + i];
                        if (p == '.' || p == word[i]) continue;
                        return false;
                    }
                }
                else
                {
                    for (var i = 0; i < Length; i++)
                    {
                        var p = _grid[X + i, Y];
                        if (p == '.' || p == word[i]) continue;
                        return false;
                    }
                }

                return true;
            }

            public override string ToString()
            {
                return GetWord();
            }

            public bool ApplyLetter(string word, int index)
            {
                ref var c = ref GetChar(index);
                if (c == '.')
                {
                    c = word[index];
                    NumSpaces--;
                    var intersection = Intersections[index];
                    if (intersection != null) intersection.NumSpaces--;
                    return true;
                }

                return c == word[index] ? false : throw new InvalidOperationException("Bad character");
            }

            public void RevertLetter(int index)
            {
                GetChar(index) = '.';
                _isVerified = false;
                NumSpaces++;
                var intersection = Intersections[index];
                if (intersection != null)
                {
                    intersection.NumSpaces++;
                    intersection._isVerified = false;
                }
            }

            public bool Verify(HashSet<string> words)
            {
                if (_isVerified) return true;
                if (!words.Contains(GetWord())) return false;
                return _isVerified = true;
            }

            private ref char GetChar(int index)
            {
                if (Vertical)
                {
                    return ref _grid[X, Y + index];
                }

                return ref _grid[X + index, Y];
            }

            public string GetWord()
            {
                var result = new char[Length];
                var i = 0;
                foreach (var c in GetLetters()) result[i++] = c;
                return new string(result);
            }
        }
    }
}
