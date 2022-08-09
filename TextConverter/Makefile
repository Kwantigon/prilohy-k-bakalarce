TARGETS = TextConverter.exe

all: $(TARGETS)

%.exe: %.cs
		mcs $< $(wildcard ~/morphodita/bindings/csharp/Ufal/MorphoDiTa/*)

.PHONY: clean
clean:
		rm -rf $(TARGETS)

