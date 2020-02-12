from itertools import groupby, count
from copy import deepcopy
import os

class Generator:
    def __init__(self, template, wordListPath):
        self.__height = len(template)
        if self.__height == 0:
            return
        self.__width = len(template[0])

        self.__grid_template = []
        for y in range(self.__height):
            grid_row = []
            self.__grid_template.append(grid_row)
            template_row = template[y].lower()
            if len(template_row) != self.__width:
                raise Exception('All rows must have the same length')
            for c in template_row:
                if c in ('*', '.') or 'a' <= c <= 'z':
                    grid_row.append(c)
                    continue

                raise Exception("Invalid character '{0}' in template row {1}".format(c, y + 1))

        if self.__width == 0:
            return

        self.__entries_template = []

        for y in range(self.__height):
            length = 0
            for x in range(self.__width):
                length = 0 if self.__has_spaces(x, y, self.__grid_template[y][x], length, False) else length + 1

            self.__add_entry(self.__width, y, length, False)

        for x in range(self.__width):
            length = 0
            for y in range(self.__height):
                length = 0 if self.__has_spaces(x, y, self.__grid_template[y][x], length, True) else length + 1

            self.__add_entry(x, self.__height, length, True)

        lengths = set()
        horz_entry_grid = []
        vert_entry_grid = []
        for y in range(self.__height):
            horz_row = []
            vert_row = []
            horz_entry_grid.append(horz_row)
            vert_entry_grid.append(vert_row)
            for x in range(self.__width):
                horz_row.append(None)
                vert_row.append(None)

        for entry in self.__entries_template:
            if entry.num_spaces > 0:
                lengths.add(entry.length)
            for i in range(entry.length):
                x, y = entry.get_coords(i)
                entry_grid = vert_entry_grid if entry.vertical else horz_entry_grid
                entry_row = entry_grid[y]
                if entry_row[x] is not None:
                    raise Exception('Grid already has an entry')
                entry_row[x] = entry

        for entry in self.__entries_template:
            if entry.vertical:
                continue
            intersect_grid = vert_entry_grid
            for i in range(entry.length):
                x, y = entry.get_coords(i)
                intersect_entry = intersect_grid[y][x]
                if intersect_entry is None:
                    continue
                entry.intersections[i] = intersect_entry.create_intersection(x, y, entry)

        groups = []
        tuplizer = lambda e: ((e.y, e.x), e)
        key_selector = lambda p: p[0]
        value_selector = lambda p: p[1]
        for g, v in groupby(sorted(map(tuplizer, self.__entries_template), key=key_selector), key=key_selector):
            groups.append(list(map(value_selector, v)))

        for g, i in zip(groups, count(1)):
            for e in g:
                e.index = i

        self.__all_words = set()
        self.__words_by_length = {}
        for length in lengths:
            self.__words_by_length[length] = []

        with open(wordListPath, 'rt') as word_list:
            for word in word_list:
                word = word.rstrip()
                words = self.__words_by_length.get(len(word))
                if words is None:
                    continue
                words.append(word)
                self.__all_words.add(word)

    def generate(self):
        return self.__generate(deepcopy(self.__grid_template), deepcopy(self.__entries_template), 0)

    def __generate(self, grid, entries, entry_index):
        if entry_index == len(entries):
            yield Crossword(entries, deepcopy(grid))
            return

        entry = entries[entry_index]
        if entry.is_complete():
            if not entry.verify(grid, self.__all_words):
                return
            for crossword in self.__generate(grid, entries, entry_index + 1):
                yield crossword
            return

        for word in self.__find_words(grid, entry):
            for crossword in self.__apply_letter(grid, entry, entries, entry_index, word, 0):
                yield crossword

    def __apply_letter(self, grid, entry, entries, entry_index, word, char_index):
        if char_index == len(word):
            first_incomplete_intersector = None
            for intersector in entry.intersections:
                if intersector is None:
                    continue
                if intersector.is_complete():
                    if not intersector.verify(grid, self.__all_words):
                        return
                    continue

                has_words = False
                for i in self.__find_words(grid, intersector):
                    has_words = True
                    break

                if not has_words:
                    return
                if first_incomplete_intersector is not None:
                    continue
                first_incomplete_intersector = intersector

            if first_incomplete_intersector is None:
                for crossword in self.__generate(grid, entries, entry_index + 1):
                    yield crossword
            else:
                for intersect_word in self.__find_words(grid, first_incomplete_intersector):
                    for crossword in self.__apply_letter(grid, first_incomplete_intersector, entries, entry_index, intersect_word, 0):
                        yield crossword

            return

        needs_revert = entry.apply_letter(grid, word, char_index)
        for crossword in self.__apply_letter(grid, entry, entries, entry_index, word, char_index + 1):
            yield crossword
        if needs_revert:
            entry.revert_letter(grid, char_index)

    def __find_words(self, grid, entry):
        for word in self.__words_by_length[entry.length]:
            if entry.matches(grid, word):
                yield word

    def __has_spaces(self, x, y, c, length, vertical):
        if c != '*':
            return False
        self.__add_entry(x, y, length, vertical)
        return True

    def __add_entry(self, x, y, length, vertical):
        if length < 2:
            return
        start_x = x if vertical else x - length
        start_y = y - length if vertical else y
        self.__entries_template.append(Entry(self.__grid_template, start_x, start_y, length, vertical))

class Entry:
    def __init__(self, grid, x, y, length, vertical):
        self.index = 0
        self.x = x
        self.y = y
        self.length = length
        self.vertical = vertical
        self.intersections = []
        self.num_spaces = 0
        for c in self.__get_letters(grid):
            self.intersections.append(None)
            if c != '.':
                continue
            self.num_spaces += 1
        self.__is_verified = self.num_spaces == 0

    def is_complete(self):
        return self.num_spaces == 0

    def get_coords(self, index):
        return (self.x, self.y + index) if self.vertical else (self.x + index, self.y)

    def create_intersection(self, x, y, entry):
        if self.vertical:
            if x != self.x:
                raise Exception('Invalid intersection')
            position = y - self.y
        else:
            if y != self.y:
                raise Exception('Invalid intersection')
            position = x - self.x

        if position < 0 or position >= self.length:
            raise Exception('Invalid intersection')
        self.intersections[position] = entry
        return self

    def __get_letters(self, grid):
        if self.vertical:
            for i in range(self.length):
                yield grid[self.y + i][self.x]
        else:
            for i in range(self.length):
                yield grid[self.y][self.x + i]

    def matches(self, grid, word):
        if self.vertical:
            for i in range(self.length):
                p = grid[self.y + i][self.x]
                if p not in ('.', word[i]):
                    return False
        else:
            for i in range(self.length):
                p = grid[self.y][self.x + i]
                if p not in ('.', word[i]):
                    return False

        return True

    def apply_letter(self, grid, word, index):
        c = self.__get_char(grid, index)
        if c == '.':
            c = word[index]
            self.__set_char(grid, index, c)
            self.num_spaces -= 1
            intersection = self.intersections[index]
            if intersection is not None:
                intersection.num_spaces -= 1
            return True

        if c == word[index]:
            return False
        raise Exception('Bad character')

    def revert_letter(self, grid, index):
        self.__set_char(grid, index, '.')
        self.__letter_reverted()
        intersection = self.intersections[index]
        if intersection is None:
            return
        intersection.__letter_reverted()

    def __letter_reverted(self):
        self.__is_verified = False
        self.num_spaces += 1

    def verify(self, grid, words):
        if self.__is_verified:
            return True
        if self.get_word(grid) not in words:
            return False
        self.__is_verified = True
        return True

    def __get_char(self, grid, index):
        return grid[self.y + index][self.x] if self.vertical else grid[self.y][self.x + index]

    def __set_char(self, grid, index, c):
        if self.vertical:
            grid[self.y + index][self.x] = c
        else:
            grid[self.y][self.x + index] = c

    def get_word(self, grid):
        return ''.join(self.__get_letters(grid))

class Crossword:
    def __init__(self, entries, grid):
        self.__entries = entries
        self.__grid = grid

    def print(self):
        print(self)

    def to_html(self, heading_text=None):
        entries_by_start_cell = dict(map(lambda e: ((e.x, e.y), e), self.__entries))
        grid_rows = ''
        for y in range(len(self.__grid)):
            grid_rows += '<tr>'
            row = self.__grid[y]
            for x in range(len(row)):
                c = row[x]
                if c == '*':
                    grid_rows += '<td class="filled"></td>'
                else:
                    entry = entries_by_start_cell.get((x, y))
                    if entry is None:
                        grid_rows += '<td></td>'
                    else:
                        grid_rows += '<td>{0}</td>'.format(entry.index)

            grid_rows += '</tr>'

        clue_formatter = lambda e: '<li value="{0}">{1}</li>'.format(e.index, e.get_word(self.__grid))
        across = ''.join(map(clue_formatter, self.__get_across_entries()))
        down = ''.join(map(clue_formatter, self.__get_down_entries()))

        heading = '' if heading_text is None else '''
        <h1>{0}</h1>'''.format(heading_text)

        return '''<html>
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
    <body>''' + heading + '''
        <table>
            <tr>
                <td><table class="grid">{0}</table></td>
                <td class="clues">
                    <table>
                        <tr>
                            <td>
                                <h2>Across</h2>
                                <ol>{1}</ol>
                            </td>
                            <td>
                                <h2>Down</h2>
                                <ol>{2}</ol>
                            </td>
                        </tr>
                    </table>
                </td>
            </tr>
        </table>
    </body>
</html>'''.format(grid_rows, across, down)

    def __get_across_entries(self):
        return filter(lambda e: not e.vertical, self.__entries)

    def __get_down_entries(self):
        return filter(lambda e: not e.vertical, self.__entries)

    def __str__(self):
        result = ''
        for y in self.__grid:
            result += ''.join(y) + os.linesep

        result += os.linesep

        clue_formatter = lambda e: '{0}: {1}'.format(e.index, e.get_word(self.__grid)) + os.linesep
        result += 'Across:' + os.linesep
        for clue in map(clue_formatter, self.__get_across_entries()):
            result += clue

        result += os.linesep

        result += 'Down:' + os.linesep
        for clue in map(clue_formatter, self.__get_down_entries()):
            result += clue

        return result
