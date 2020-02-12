import crossword

# Create a template for the crossword - the template must be rectangular (all rows the same length)
# '*' is a black square
# '.' is an empty square
# 'a'..'z' are fixed letters
template = [
    's..e.',
    '.*...',
    '.l.r.',
    '...*.',
    '.b..s'
]

# Create a crossword generator from the given template and word list file
generator = crossword.Generator(template, '../Crossword/words58k.txt')

# Create an iterable from the generator using the generate() method, and begin iterating it
crossword_iterator = iter(generator.generate())

# Get the first crossword from the iterator
a_crossword = next(crossword_iterator)

# Print the crossword
print(a_crossword)

# Format the crossword as HTML and write to a file
with open('crossword.html', 'wt') as file:
    file.write(a_crossword.to_html('An Example Crossword'))
