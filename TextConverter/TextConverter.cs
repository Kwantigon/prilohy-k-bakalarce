using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Ufal.MorphoDiTa;

namespace TextConverter {
	class Constants {
		public const string TAGGER_MODEL = "./files-to-load/czech-morfflex-pdt-161115.tagger";
		public const string MORPHO_DICTIONARY = "./files-to-load/czech-morfflex-161115.dict";
		public const string REPLACEMENT_WORDS_FILE = "./files-to-load/replacement-words.dict";
		public const string FILLER_WORDS_FILE = "./files-to-load/filler-words.xml";

		public const int FILLER_CHANCE = 30;
		public const int REPETITION_CHANCE = 10;

		// POS is an abbreviation for "part of speech",
		// which is the first character in the tag.
		public const int POS_INDEX = 0;
		public const char POS_ADJECTIVE = 'A';
		public const char POS_NOUN = 'N';
		public const char POS_PRONOUN = 'P';
		public const char POS_VERB = 'V';
		public const char POS_PUNCTUATION = 'Z';

		public const int SUBPOS_INDEX = 1;
		public const char SUBPOS_POSSESSIVE = 'S';

		public const int GENDER_INDEX = 2;
		public const char GENDER_M_ANIMATE = 'M';
		public const char GENDER_M_INANIMATE = 'I';
		public const char GENDER_NEUTER = 'N';
		public const char GENDER_FEMININE = 'F';

		public const int NUMBER_INDEX = 3;
		public const char NUMBER_SINGULAR = 'S';
		public const char NUMBER_PLURAL = 'P';

		public const int CASE_INDEX = 4;

		public const int VARIANT_INDEX = 14;
		public const char VARIANT_BASIC = '-';
		public const char VARIANT_COLLOQUIAL = '6';
	}

	class TextConverter {
		private Tagger tagger; // Morphodita's tagger.
		private Morpho morpho; // Morphodita's morphological generator.
		private string taggerModelFile { get; }
		private string morphoDictionaryFile { get; }
		private string fillerWordsFile { get; }
		private string replacementWordsFile { get; }

		public int FillerChance { get; private set; } = Constants.FILLER_CHANCE;
		public int RepetitionChance { get; private set; } = Constants.REPETITION_CHANCE;

		public bool SetFillerChance(int fillerChance) {
			if ((fillerChance < 0) || (fillerChance > 100)) {
				return false;
			} else {
				this.FillerChance = fillerChance;
				return true;
			}
		}
		public bool SetRepetitionChance(int repetitionChance) {
			if ((repetitionChance < 0) || (repetitionChance > 100)) {
				return false;
			} else {
				this.RepetitionChance = repetitionChance;
				return true;
			}
		}

		/** These 2 bool variables are used for loading the tagger/morpho generator.
		*	If the first time trying to load the tagger or morpho generator fails,
		*	the corresponding variable will be set to true.
		*	If either variable is true, there is no point in trying to load again.
		*/
		private bool taggerAlreadyFailedToLoad = false;
		private bool morphoAlreadyFailedToLoad = false;

		private Dictionary<string, string> replacementWords;
		private List<string> fillerWordsBeginning;	// filler words used at the beginning of a sentence.
		private List<string> fillerWordsMiddle;		// filler words used in the middle of a sentence.
		private List<string> fillerWordsEnd;		// filler words used at the end of a sentence.
		private enum PositionInSentence {
			Beginning,
			Middle,
			End
		}

		public TextConverter() {
			this.taggerModelFile = Constants.TAGGER_MODEL;
			this.morphoDictionaryFile = Constants.MORPHO_DICTIONARY;
			this.replacementWordsFile = Constants.REPLACEMENT_WORDS_FILE;
			this.fillerWordsFile = Constants.FILLER_WORDS_FILE;
		}

		public TextConverter(string taggerModelFile, string morphoDictionaryFile,
					string replacementWordsFile, string fillerWordsFile) {
			this.taggerModelFile = taggerModelFile;
			this.morphoDictionaryFile = morphoDictionaryFile;
			this.replacementWordsFile = replacementWordsFile;
			this.fillerWordsFile = fillerWordsFile;
		}

		/** Load all the needed components of the converter.
		*	Returns true if all components load successfully.
		*	False if at least one thing fails to load.
		*/
		public bool Initialize() {
			// Load the tagger.
			if (taggerAlreadyFailedToLoad) {
				return false;
			} else {
			// (!taggerAlreadyFailedToLoad)
				if (tagger == null) {
					tagger = Tagger.load(taggerModelFile);
					if (tagger == null) {
						Console.Error.WriteLine("Failed to load tagger from {0}", taggerModelFile);
						taggerAlreadyFailedToLoad = true;
						return false;
					}
				} else {
					Console.Error.WriteLine("Tagger successfully loaded.");
				}
			}

			// Load the morho.
			if (morphoAlreadyFailedToLoad) {
				return false;
			} else {
			// (morpho == null && !morphoAlreadyFailedToLoad)
				if (morpho == null) {
					morpho = Morpho.load(morphoDictionaryFile);
					if (morpho == null) {
						Console.Error.WriteLine("Failed to load morpho from {0}", morphoDictionaryFile);
						morphoAlreadyFailedToLoad = true;
						return false;
					} else {
						Console.Error.WriteLine("Morpho successfully loaded.");
					}
				}
			}

			// Load the replacementWords.
			if (replacementWords == null) {
				replacementWords = new Dictionary<string, string>();
				StreamReader replacementWordsReader = null;
				try {
					replacementWordsReader = new StreamReader(replacementWordsFile);
					string[] words = new string[2];
					string line = replacementWordsReader.ReadLine();
					while (line != null) {
						// Lines starting with '#' are comments.
						if (line[0] != '#') {
							words = line.Split('\t');
							if (words.Length == 2) {
								replacementWords[words[0]] = words[1];
								Console.Error.WriteLine("[{0}] = {1}", words[0], words[1]);
							} else {
								Console.Error.WriteLine("There are {0} words on this line.", words.Length);
							}
						}
						line = replacementWordsReader.ReadLine();
					}
				}
				catch (IOException exception) {
					Console.Error.WriteLine("Caught and exception while trying to read the replacementWords file: {0}",
																						exception);
					return false;
				}
				finally {
					replacementWordsReader?.Dispose();
				}
			}

			if ((fillerWordsBeginning == null) && (fillerWordsMiddle == null) && (fillerWordsEnd == null)) {
				fillerWordsBeginning = new List<string>();
				fillerWordsMiddle = new List<string>();
				fillerWordsEnd = new List<string>();

				const string beginningOpeningTag = "<beginning>";
				const string beginningClosingTag = "</beginning>";
				const string middleOpeningTag = "<middle>";
				const string middleClosingTag = "</middle>";
				const string endOpeningTag = "<end>";
				const string endClosingTag = "</end>";
				string expectedTag = beginningOpeningTag;
				StreamReader fillerWordsReader = null;
				try {
					fillerWordsReader = new StreamReader(fillerWordsFile);
					string word = fillerWordsReader.ReadLine(); // One word or tag is expected per line.
					List<string> listToFill = null;
					while (word != null) {
						switch (word) {
							case beginningOpeningTag:
								if (expectedTag == beginningOpeningTag) {
									listToFill = fillerWordsBeginning;
									expectedTag = beginningClosingTag;
								} else {
									Console.Error.WriteLine("Expected tag \"{0}\" but got \"{1}\".", expectedTag, word);
									return false; // Syntax error in the fillerWordsFile.
								}
								break;
							case beginningClosingTag:
								if (expectedTag == beginningClosingTag) {
									listToFill = null;
									expectedTag = middleOpeningTag;
								} else {
									Console.Error.WriteLine("Expected tag \"{0}\" but got \"{1}\".", expectedTag, word);
									return false; // Syntax error in the fillerWordsFile.
								}
								break;
							case middleOpeningTag:
								if (expectedTag == middleOpeningTag) {
									listToFill = fillerWordsMiddle;
									expectedTag = middleClosingTag;
								} else {
									Console.Error.WriteLine("Expected tag \"{0}\" but got \"{1}\".", expectedTag, word);
									return false; // Syntax error in the fillerWordsFile.
								}
								break;
							case middleClosingTag:
								if (expectedTag == middleClosingTag) {
									listToFill = null;
									expectedTag = endOpeningTag;
								} else {
									Console.Error.WriteLine("Expected tag \"{0}\" but got \"{1}\".", expectedTag, word);
									return false; // Syntax error in the fillerWordsFile.
								}
								break;
							case endOpeningTag:
								if (expectedTag == endOpeningTag) {
									listToFill = fillerWordsEnd;
									expectedTag = endClosingTag;
								} else {
									Console.Error.WriteLine("Expected tag \"{0}\" but got \"{1}\".", expectedTag, word);
									return false; // Syntax error in the fillerWordsFile.
								}
								break;
							case endClosingTag:
								if (expectedTag == endClosingTag) {
									listToFill = null;
									expectedTag = null;
									// At this point, 'expectedTag' and 'listToFill' shouldn't be needed any more.
									// So referencing them should throw an exception.
								} else {
									Console.Error.WriteLine("Expected tag \"{0}\" but got \"{1}\".", expectedTag, word);
									return false; // Syntax error in the fillerWordsFile.
								}
								break;
							default:
								if (String.IsNullOrWhiteSpace(word)) {
									break;
								}
								if (listToFill == null) {
									// Syntax error in the fillerWordsFile
									Console.Error.WriteLine("Did not get an opening tag. The variable \"listToFill\" is null");
									Console.Error.WriteLine("Expected tag: {0}", expectedTag);
									return false;
								} else {
									listToFill.Add(word);
								}
								break;
						}

						word = fillerWordsReader.ReadLine();
					}
				}
				catch (IOException exception) {
					Console.Error.WriteLine("Caught an exception while trying to read the filler words file: {0}",
																						exception);
					return false;
				}
				finally {
					fillerWordsReader?.Dispose();
				}
			}

			// All components loaded successfully.
			return true;
		}

		/** Convert the given input text into a written speech that contains colloquial forms etc.
		*/
		public int Convert(TextReader inputReader, TextWriter outputWriter) {
			Console.Error.WriteLine("TextConverter.Convert() start.");

			Tokenizer tokenizer = tagger.newTokenizer();
			if (tokenizer == null) {
				Console.Error.WriteLine("Could not get a tokenizer for the supplied model!");
				return 1;
			}

			// Read one line from the input, tag it and convert to colloquial speech.
			// One truecased sentence is expected per line.
			string inputText = inputReader.ReadLine();
			while (inputText != null) {
				tokenizer.setText(inputText);

				// For storing the sentence returned by tokenizer.nextSentence().
				Forms sentence = new Forms();
				// Used to locate each token in the sentence returned by tokenizer.nextSentence().
				TokenRanges tokenRanges = new TokenRanges();
				// For storing the output from tagger.tag().
				TaggedLemmas taggedLemmas = new TaggedLemmas();

				// Tag and process the sentence
				if (tokenizer.nextSentence(sentence, tokenRanges)) {
					tagger.tag(sentence, taggedLemmas);
					Console.Error.WriteLine("Sentence tagged.");

					// Decide on filler words.
					// "Proste, vlastne, ...
					/* Possible upgrade:
						decide on filler words after processing each word in the sentence
						for example "uprimne receno takze .... " sounds strange
						so filler words could be decided based on what words I have seen in the sentence
					*/
					Random rng = new Random();
					string fillerWord = null;
					int fillerWordIndex = -1;
					// FillerChance% of adding filler words.
					if (rng.Next(1, 101) <= FillerChance) {
						// Decide randomly whether to add to the beginning or middle of the sentence.
						int randomPosition = rng.Next(2); // 0 means beginning, 1 means middle.
						Console.Error.WriteLine("randomPosition: {0}", randomPosition);
						PositionInSentence position = (PositionInSentence)randomPosition;
						Console.Error.WriteLine("Filler position: {0}", position);
						fillerWord = GetFillerWord(position);
						Console.Error.WriteLine("Filler word: \"{0}\".", fillerWord);
						// Get the index, where the filler word should be inserted.
						// I'm assuming that every sentence ends with a punctuation.
						// The filler word gets inserted before the original word at the fillerWordIndex.
						// For example:
						//	original sentence: A B C.
						//	filler word: F
						//	filler word index: 1
						//	after adding filler: A F B C.
						switch (position) {
							case PositionInSentence.Beginning:
								fillerWordIndex = 0;
								break;
							case PositionInSentence.Middle:
								// Some sentences might not have a punctuation at the end.
								// For example a sentence: "Yes"
								// Would throw an exception.
								if (sentence.Count - 1 >= 1) {
									fillerWordIndex = rng.Next(1, sentence.Count - 1);
								} else {
									fillerWordIndex = 1;
								}
								break;
							case PositionInSentence.End:
								fillerWordIndex = sentence.Count - 1;
								break;
						}
						Console.Error.WriteLine("Filler word index: {0}", fillerWordIndex);
					}

					int repeatWordIndex = -1; // I just need to initialize it to some invalid value, that's why -1.
					if (rng.Next(1, 101) < RepetitionChance) {
						// 10% chance of repeating one word in the sentence.
						repeatWordIndex = rng.Next(sentence.Count - 1);
												// minus one to avoid the punctuation at the end.
						Console.Error.WriteLine("Reapeat word index: {0}", repeatWordIndex);
					}

					// Process each word in the sentence that has been tagged by the tagger.
					List<string> outputSentenceWords = new List<string>();
					for (int i = 0; i < sentence.Count; i++) {
						string originalWord = sentence[i];
						string lemma = taggedLemmas[i].lemma;
						string tag = taggedLemmas[i].tag;
						// Debug print.
						Console.Error.WriteLine(nameof(originalWord) + ": " + originalWord + ", " +
											nameof(lemma) + ": " + lemma + ", " +
											nameof(tag) + ": " + tag);

						// Check the dictinary for word replacement.
						string outputWord = originalWord;

						bool replacementTookPlace = false;
						if (replacementWords.ContainsKey(lemma) || replacementWords.ContainsKey(originalWord)) {
							Console.Error.WriteLine("Found the word \"{0}\" in the dictionary.", originalWord);
							if (rng.Next(10) < 6) {
								outputWord = GetReplacementWord(originalWord, lemma, tag);
								Console.Error.WriteLine("Replacing \"{0}\" with \"{1}\".", originalWord, outputWord);
								replacementTookPlace = true;
							}
							else {
								Console.Error.WriteLine("It has been randomly decided that replacement for \"{0}\" won't take place.", originalWord);
							}
						}

						char POS = tag[Constants.POS_INDEX];
						char variant = tag[Constants.VARIANT_INDEX];
						// If the word is in it's basic variant,
						// call the appropriate function to change it
						// to the colloquial variant.
						//
						// It is important to give 'outputWord' to the Change... methods,
						// not 'originalWord'.
						// Because replacement could have taken place and it is stored in 'outputWord'
						//
						// outputWord is either the originalWord or the replacement found in the dictionary.
						// It cannot be anything else.
						switch (POS) {
							case Constants.POS_ADJECTIVE:
								// For example "bílého psa" -> "bílýho psa".
								if (replacementTookPlace) {
									outputWord = ChangeToColloquialVariant(outputWord, outputWord, tag);
									// Giving the outputWord as lemma, because the replacements are stored
									// in the dictionary in the lemma form.
								} else {
									outputWord = ChangeToColloquialVariant(originalWord, lemma, tag);
								}
								break;
							case Constants.POS_NOUN:
								if (replacementTookPlace) {
									outputWord = ChangeNounToColloquial(outputWord, outputWord, tag);
									// Similarly to adjectives:
									// Giving the outputWord as lemma, because the replacements are stored
									// in the dictionary in the lemma form.
								} else {
									outputWord = ChangeNounToColloquial(originalWord, lemma, tag);
								}
								break;
							case Constants.POS_PRONOUN:
								// For example "méha psa" -> "mýho psa".
								char subPOS = tag[Constants.SUBPOS_INDEX];
								if (subPOS == Constants.SUBPOS_POSSESSIVE) {
									outputWord = ChangeToColloquialVariant(outputWord, lemma, tag);
								}
								break;
							case Constants.POS_PUNCTUATION:
								if (i == repeatWordIndex) {
									// The index for punctuation was chosen to be repeated.
									// Try repeating the next word instead.
									repeatWordIndex++;
								}
								break;
							default:
								break;
						}
						outputSentenceWords.Add(outputWord);
					}
					// Put the words together to form a sentence.
					// The input was supposed to be tokenized and truecased,
					// so the output will also be tokenized and truecased.
					StringBuilder outputSentenceBuilder = new StringBuilder();
					for (int i = 0; i < outputSentenceWords.Count; i++) {
						// Add filler word if needed.
						if (i == fillerWordIndex) {
							outputSentenceBuilder.Append(fillerWord + " ");
						}
						// Then add the actual word.
						outputSentenceBuilder.Append(outputSentenceWords[i] + " ");
						// Repeat the word if needed.
						if (i == repeatWordIndex) {
							outputSentenceBuilder.Append(outputSentenceWords[i] + " ");
						}
					}
					outputWriter.Write(outputSentenceBuilder.ToString());
					outputWriter.WriteLine();
				} else {
					// tokenizer.nextSentence() == false
					Console.Error.WriteLine("tokenizer.nextSentence() returned false. The sentence should be \"{0}\"",
																								inputText);
				}
				inputText = inputReader.ReadLine();
			}

			outputWriter.WriteLine();
			return 0;
		}

		/** Use Morphodita to generate the colloquial form of the given 'originalWord'.
		*	If Morphodita doesn't generate a colloquial form, simply return originalWord as is.
		*/
		private string ChangeToColloquialVariant(string originalWord, string lemma, string tag) {
			StringBuilder tagBuilder = new StringBuilder(tag);
			tagBuilder[Constants.VARIANT_INDEX] = Constants.VARIANT_COLLOQUIAL;
			string modifiedTag = tagBuilder.ToString();
			TaggedLemmasForms generatedResult = new TaggedLemmasForms();
			Console.Error.WriteLine("morpho.generate({0}, {1}, Morpho.GUESSER, generatedResult);", lemma, modifiedTag);
			morpho.generate(lemma, modifiedTag, Morpho.GUESSER, generatedResult);
			Console.Error.WriteLine("generatedResult.Count == {0}", generatedResult.Count);
			if (generatedResult.Count == 0) {
				// The morphological generator didn't manage to generate anything so just use the originalWord
				return originalWord;
				// Possible upgrade is to try other colloquial forms. Right now I'm only generating variant with
				// tag number 6. Could also try 5, 7, ...
			} else {
				// I expect the morphological generator to generate at most one word.
				// But if it generates more, just use the first one.
				// I could randomly pick one of the generated forms for more variety.
				TaggedForms generatedForms = generatedResult[0].forms;
				Console.Error.WriteLine("Generated word: {0}.", generatedForms[0].form);
				return generatedForms[0].form;
			}
		}

		/**
		*	In this version, this method returns a colloquial form for the word 'originalWord'.
		*	But only if the word is in plural and 7th case.
		*/
		private string ChangeNounToColloquial(string originalWord, string lemma, string tag) {
			StringBuilder tagBuilder = new StringBuilder(tag);
			// Create the tag for morphological generation
			// by replacing the variant tag with a the tag for colloquial form of the word.

			char number = tag[Constants.NUMBER_INDEX];
			char wordCase = tag[Constants.CASE_INDEX];
			if ((number == Constants.NUMBER_PLURAL) && (wordCase == '7')) {
				tagBuilder[Constants.VARIANT_INDEX] = Constants.VARIANT_COLLOQUIAL;
			} else {
				return originalWord;
			}

			string modifiedTag = tagBuilder.ToString();
			TaggedLemmasForms generatedResult = new TaggedLemmasForms();
			Console.Error.WriteLine("morpho.generate({0}, {1}, Morpho.GUESSER, generatedResult);", lemma, modifiedTag);
			morpho.generate(lemma, modifiedTag, Morpho.GUESSER, generatedResult);
			Console.Error.WriteLine("generatedResult.Count == {0}", generatedResult.Count);
			if (generatedResult.Count == 0) {
				// The morphological generator didn't manage to generate anything so just use the originalWord
				return originalWord;
				// Possible upgrade is to try other colloquial forms. Right now I'm only generating variant with
				// tag number 6. Could also try 5, 7, ...
			} else {
				// I expect the morphological generator to generate at most one word.
				// But if it generates more, just use the first one.
				TaggedForms generatedForms = generatedResult[0].forms;
				Console.Error.WriteLine("Generated word: {0}.", generatedForms[0].form);
				return generatedForms[0].form;
			}
		}

		/** Return a random filler word from the list of filler words.
		*/
		private string GetFillerWord(PositionInSentence position) {
			Random rng = new Random();
			int k = 0;
			switch (position) {
				case PositionInSentence.Beginning:
					k = rng.Next(fillerWordsBeginning.Count);
					return fillerWordsBeginning[k];
				case PositionInSentence.Middle:
					k = rng.Next(fillerWordsMiddle.Count);
					return fillerWordsMiddle[k];
				case PositionInSentence.End:
					k = rng.Next(fillerWordsEnd.Count);
					return fillerWordsEnd[k];
				default:
					return null;
			}
		}

		private string GetReplacementWord(string originalWord, string originalLemma, string tag) {
			string replacementWord = "";
			if (replacementWords.ContainsKey(originalWord)) {
				replacementWord = replacementWords[originalWord];
				//
			} else if (replacementWords.ContainsKey(originalLemma)) {
				replacementWord = replacementWords[originalLemma];
			} else {
				return originalWord;
			}

			switch(tag[Constants.POS_INDEX]) {
				case Constants.POS_NOUN:
					TaggedLemmasForms generatedNoun = new TaggedLemmasForms();
					Console.Error.WriteLine("morpho.generate({0}, {1},...);", replacementWord, tag);
					morpho.generate(replacementWord, tag, Morpho.GUESSER, generatedNoun);
					Console.Error.WriteLine("generatedNoun.Count == {0}", generatedNoun.Count);
					if (generatedNoun.Count > 0) {
						TaggedForms generatedForms = generatedNoun[0].forms;
						string generatedReplacement = generatedForms[0].form;
						Console.Error.WriteLine("The word \"{0}\" can be replaced by the word \"{1}\"",
														originalWord, generatedReplacement);
						return generatedReplacement;
					} else {
						// If failed to generate the correct form of the replacement,
						// it probably means the replacement has a different gender.
						// Just return the lemma for the replacement.
						// This can obviously be improved.
						return replacementWord;
					}
				case Constants.POS_VERB:
					TaggedLemmasForms generatedVerb = new TaggedLemmasForms();
					Console.Error.WriteLine("morpho.generate({0}, {1},...);", replacementWord, tag);
					morpho.generate(replacementWord, tag, Morpho.GUESSER, generatedVerb);
					Console.Error.WriteLine("generatedVerb.Count == {0}", generatedVerb.Count);
					if (generatedVerb.Count > 0) {
						TaggedForms generatedForms = generatedVerb[0].forms;
						string generatedReplacement = generatedForms[0].form;
						Console.Error.WriteLine("The word \"{0}\" can be replaced by the word \"{1}\"",
														originalWord, generatedReplacement);
						return generatedReplacement;
					} else {
						// If failed to generate the correct form of the replacement word,
						// just return the original.
						return originalWord;
					}
				default:
					return replacementWord;
			}
		}
	}

	class Program {
		public static void EchoUsage() {
			Console.WriteLine("Usage: TextConverter.exe [OPTIONS]");
			Console.WriteLine("Convert text from standard input into colloquial text similar to an unprepared speech.");
			Console.WriteLine();
			Console.WriteLine("Options:");
			Console.WriteLine("    --help\t\t\t\t\tDisplay this help and exit.");
			Console.WriteLine("    --tagger-model [TAGGER_FILE]\t\tLocation of the tagger model file for MorphoDiTa.");
			Console.WriteLine("    --morpho-dictionary [DICTIONARY_FILE]\tLocation of the dictionary file for MorphoDiTa.");
			Console.WriteLine("    --replacement-words [REPLACEMENT_FILE]\tLocation of the file containing words to be replaced\n\t\t\t\t\t\tand their colloquial equivalents.");
			Console.WriteLine("    --filler-words [FILLER_FILE]\t\tLocation of the file containing filler words\n\t\t\t\t\t\tto be added to the sentences.");
			Console.WriteLine("    --filler-chance [0 - 100]\t\t\tSet the chance of adding a filler word to each sentence.");
			Console.WriteLine("    --repetition-chance [0 - 100]\t\tSet the chance of repeating one random word in each sentence.");

		}
		public static int Main(string[] args) {
			/**/
			string taggerModel = Constants.TAGGER_MODEL;
			string morphoDictionary = Constants.MORPHO_DICTIONARY;
			string replacementWordsFile = Constants.REPLACEMENT_WORDS_FILE;
			string fillerWordsFile = Constants.FILLER_WORDS_FILE;
			int fillerChance = Constants.FILLER_CHANCE;
			int repetitionChance = Constants.REPETITION_CHANCE;

			for (int i = 0; i < args.Length; i++) {
				switch (args[i]) {
					case "--help":
					case "-h":
						EchoUsage();
						return 0;
					case "--tagger-model":
						i++;
						if (i < args.Length) {
							taggerModel = args[i];
						} else {
							Console.Error.WriteLine("args[{0}] is out of range. No tagger model was given.", i);
							EchoUsage();
							return 2;
						}
						break;
					case "--morpho-dictionary":
						i++;
						if (i < args.Length) {
							morphoDictionary = args[i];
						} else {
							Console.Error.WriteLine("args[{0}] is out of range. No morpho dictionary was given.", i);
							EchoUsage();
							return 2;
						}
						break;
					case "--replacement-words":
						i++;
						if (i < args.Length) {
							replacementWordsFile = args[i];
						} else {
							Console.Error.WriteLine("args[{0}] is out of range. No replacement words file was given.", i);
							EchoUsage();
							return 2;
						}
						break;
					case "--filler-words":
						i++;
						if (i < args.Length) {
							fillerWordsFile = args[i];
						} else {
							Console.Error.WriteLine("args[{0}] is out of range. No filler words file was given.", i);
							EchoUsage();
							return 2;
						}
						break;
					case "--filler-chance":
						i++;
						if (i < args.Length) {
							try {
								fillerChance = Int32.Parse(args[i]);
							} catch (FormatException fe) {
								Console.Error.WriteLine("Int32.Parse(args[{0}]) couldn't parse the string \"{1}\".",
																			i,							args[i]);
								Console.Error.WriteLine(fe);
								EchoUsage();
								return 2;
							}
						} else {
							Console.Error.WriteLine("args[i] is out of range. No filler chance was given.");
							return 2;
						}
						break;
					case "--repetition-chance":
						i++;
						if (i < args.Length) {
							try {
								repetitionChance = Int32.Parse(args[i]);
							} catch (FormatException fe) {
								Console.Error.WriteLine("Int32.Parse(args[{0}]) couldn't parse the string \"{1}\".",
																			i,							args[i]);
								Console.Error.WriteLine(fe);
								EchoUsage();
								return 2;
							}
						} else {
							Console.Error.WriteLine("args[i] is out of range. No repetition chance was given.");
							EchoUsage();
							return 2;
						}
						break;
					default:
						Console.Error.WriteLine("Unknown parameter: \"{0}\".", args[i]);
						EchoUsage();
						return 2;
				}
			}
			TextConverter converter = new TextConverter(taggerModel, morphoDictionary,
								replacementWordsFile, fillerWordsFile);
			if (converter.Initialize()) {
				converter.SetFillerChance(fillerChance);
				converter.SetRepetitionChance(repetitionChance);
				Console.Error.WriteLine("FillerChance: {0}, RepetitionChance: {1}", converter.FillerChance, converter.RepetitionChance);
				converter.Convert(Console.In, Console.Out);
			} else {
				Console.Error.WriteLine("Could not initialize the TextConverter.");
				return 1;
			}
			/**/

			return 0;
		}
	}
}
