using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AltEncryptionLingoScript : MonoBehaviour {

    public KMBombModule modSelf;
    public KMColorblindMode colorblindMode;
    public KMAudio mAudio;
    public KMSelectable[] SqButtonSels;
    public KMSelectable[] LetterArrowSels;
    public KMSelectable[] PastSubArrowSels;
    public KMSelectable QuerySelectable;

    public MeshRenderer[] SqButtonDisplayRenderers, QueryResultRenderers, QueryImgRenderers;
    public TextMesh[] ScreenText, colorblindLEDText;

    public Texture[] MaritimeTextures;
    public Texture[] BrailleTextures;
    public Texture[] BoozleglyphTextures1;
    public Texture[] BoozleglyphTextures2;
    public Texture[] BoozleglyphTextures3;
    public Texture[] PigpenTextures;
    public Texture[] SignLanguageTextures;
    public Texture[] SemaphoreTextures;
    public Texture[] ZoniTextures;
    public Texture[] LombaxTextures;
    public Texture[] SGATextures;
    public Texture deleteTexture;
    public Material[] possibleColors;
    public string[] possibleColorNames;
    public Material inactiveColor;
    protected enum PossibleResponse
    {
        Absent,
        Correct,
        Almost
    }
    protected enum PossibleQuirks
    {
        None,
        Fibble,
        Xordle,
        Symble,
        FiveOhOh
    }
    readonly static PossibleQuirks[] allPossibleQuirks = {
        PossibleQuirks.Fibble,
        PossibleQuirks.Xordle,
        PossibleQuirks.FiveOhOh,
        PossibleQuirks.Symble,
    };
    protected enum PossibleEncryptions
    {
        None = 0,
        Maritime,
        Braille,
        BoozleSet1,
        BoozleSet2,
        BoozleSet3,
        Pigpen,
        SignLanguage,
        Semaphore,
        Zoni,
        Lombax,
        StandardGalacticAlphabet
    }
    readonly static PossibleEncryptions[] allPossibleEncryptions = {
        PossibleEncryptions.BoozleSet1,
        PossibleEncryptions.BoozleSet2,
        PossibleEncryptions.BoozleSet3,
        PossibleEncryptions.Braille,
        PossibleEncryptions.Lombax,
        PossibleEncryptions.Maritime,
        PossibleEncryptions.Pigpen,
        PossibleEncryptions.Semaphore,
        PossibleEncryptions.SignLanguage,
        PossibleEncryptions.StandardGalacticAlphabet,
        PossibleEncryptions.Zoni,
    };
    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    protected class QueryResponse
    {
        public string wordQueried;
        public IEnumerable<PossibleResponse> displayResponse;
        public IEnumerable<PossibleResponse> actualResponse;
        public IEnumerable<PossibleEncryptions> encryptionTypes;
    }

    readonly static Dictionary<PossibleEncryptions, Vector3> possibleLocalScaleModifiers = new Dictionary<PossibleEncryptions, Vector3>()
    {
        { PossibleEncryptions.Maritime, new Vector3(0.125f, 0.125f, 0.1f) },
        { PossibleEncryptions.Braille, new Vector3(0.085f, 0.125f, 0.1f) },
        { PossibleEncryptions.BoozleSet1, new Vector3(0.125f, 0.125f, 0.1f) },
        { PossibleEncryptions.BoozleSet2, new Vector3(0.125f, 0.125f, 0.1f) },
        { PossibleEncryptions.BoozleSet3, new Vector3(0.125f, 0.125f, 0.1f) },
        { PossibleEncryptions.Pigpen, new Vector3(0.125f, 0.125f, 0.1f) },
        { PossibleEncryptions.SignLanguage, new Vector3(0.175f, 0.175f, 0.1f) },
        { PossibleEncryptions.Zoni, new Vector3(0.175f, 0.175f, 0.1f) },
        { PossibleEncryptions.Lombax, new Vector3(0.15f, 0.15f, 0.1f) },
        { PossibleEncryptions.StandardGalacticAlphabet, new Vector3(0.15f, 0.15f, 0.1f) },
    };
    readonly static Dictionary<PossibleQuirks, string> quirkDescriptions = new Dictionary<PossibleQuirks, string>()
    {
        { PossibleQuirks.Fibble, "Exactly one of the letters in each word you guess will lie about its result." },
        { PossibleQuirks.Xordle, "Two words, that have no letters in common, will need to be guessed correctly. Each word guessed will show the \"xored\" result from the two correct words. Successfully finding one word will make the module behave like the original counterpart until the other word is found." },
        { PossibleQuirks.Symble, "The response is flipped so that the word you guessed is treated as the answer and the answer is treated as a guess. In addition, other colors may show up that do not accurately represent Encryption Lingo." },
        { PossibleQuirks.FiveOhOh, "Wordle, but it is Mastermind. If you know how Mastermind works, this is less of a challenge for you." },
    };

    static int ModuleIDCnt;
    int ModuleID;
    int curPanelIDx, curQueryIdx;
    List<QueryResponse> allQueries;

    int[] alphabetOrdering, queryColorIdxes = Enumerable.Range(0, 3).ToArray();
    PossibleEncryptions[] selectedEncryptionTypes = new PossibleEncryptions[26];
    PossibleQuirks selectedQuirk;
    IEnumerator squareBtnAnim;
    bool moduleSolved, interactable, colorblindDetected;
    string curWord = "";
    List<PossibleEncryptions> curEncryptionsLetter;
    List<string> correctWords;
    List<int> blacklistIdxesCorrect, forceQueryRevealIdx;
    void QuickLog(string value, params object[] args)
    {
        Debug.LogFormat("[Faulty Encryption Lingo #{0}] {1}", ModuleID, string.Format(value, args));
    }
    void QuickLogDebug(string value, params object[] args)
    {
        Debug.LogFormat("<Faulty Encryption Lingo #{0}> {1}", ModuleID, string.Format(value, args));
    }
    // Use this for initialization
    void Start() {
        ModuleID = ++ModuleIDCnt;

        modSelf.OnActivate += delegate { StartCoroutine(squareBtnAnim); interactable = true; };
        for (var x = 0; x < LetterArrowSels.Length; x++)
        {
            var y = x;
            LetterArrowSels[x].OnInteract += delegate {
                LetterArrowSels[y].AddInteractionPunch(0.5f);
                mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, LetterArrowSels[y].transform);
                HandleLetterScroll(2 - y);
                return false;
            };
        }
        for (var x = 0; x < PastSubArrowSels.Length; x++)
        {
            var y = x;
            PastSubArrowSels[x].OnInteract += delegate {
                PastSubArrowSels[y].AddInteractionPunch(0.5f);
                mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, PastSubArrowSels[y].transform);
                HandleQueryScroll(2 * y - 1);
                return false;
            };
        }
        for (var x = 0; x < SqButtonSels.Length; x++)
        {
            var y = x;
            SqButtonSels[x].OnInteract += delegate {
                SqButtonSels[y].AddInteractionPunch(0.5f);
                mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, SqButtonSels[y].transform);
                HandleLetterPress(y);
                return false;
            };
        }
        QuerySelectable.OnInteract += delegate {
            QuerySelectable.AddInteractionPunch(0.5f);
            mAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, QuerySelectable.transform);
            HandleQuery();
            return false;
        };
        PrepModule();
        try
        {
            colorblindDetected = colorblindMode.ColorblindModeActive;
        }
        catch
        {
            colorblindDetected = false;
        }
    }
    void PrepModule()
    {
        allQueries = new List<QueryResponse>();
        correctWords = new List<string>();
        curEncryptionsLetter = new List<PossibleEncryptions>();
        blacklistIdxesCorrect = new List<int>();
        forceQueryRevealIdx = new List<int>();
        GenerateScramble(true);
        squareBtnAnim = HandleDisplaySqAnim();
        for (var x = 0; x < SqButtonDisplayRenderers.Length; x++)
        {
            SqButtonDisplayRenderers[x].enabled = false;
        }
        for (var x = 0; x < QueryImgRenderers.Length; x++)
        {
            QueryImgRenderers[x].enabled = false;
            colorblindLEDText[x].text = "";
            ScreenText[x].text = "";
        }
        selectedQuirk = allPossibleQuirks.PickRandom();
        QuickLog("Selected Quirk: {0}", selectedQuirk.ToString());
        QuickLog("Description: {0}", quirkDescriptions.ContainsKey(selectedQuirk) ? quirkDescriptions[selectedQuirk] : "N/A");
        var shuffledWords = Data.GenerousWordList.ToArray().Shuffle();
        correctWords.Add(shuffledWords.PickRandom());
        if (selectedQuirk == PossibleQuirks.Xordle)
        {
            shuffledWords = shuffledWords.Where(a => !a.Any(b => correctWords.Single().Contains(b))).ToArray().Shuffle();
            correctWords.Add(shuffledWords.PickRandom());
        }
        else if (selectedQuirk == PossibleQuirks.Symble)
        {
            while (queryColorIdxes.All(a => Enumerable.Range(0, 3).Contains(a)))
            {
                queryColorIdxes = Enumerable.Range(0, possibleColors.Length).ToArray().Shuffle().Take(3).ToArray();
            }
            QuickLog("Letters not present in the answer will be shown in this color: {0}", possibleColorNames[queryColorIdxes[0]]);
            QuickLog("Correct letters in the correct position will be shown in this color: {0}", possibleColorNames[queryColorIdxes[1]]);
            QuickLog("Correct letters not in the correct position will be shown in this color: {0}", possibleColorNames[queryColorIdxes[2]]);
        }
        QuickLog("Selected Correct Word(s): {0}", correctWords.Join(", "));
        QuickLog("Because this module heavily scrambles the encryption types for all letters, it will be only shown in the unfiltered logs, going in reading order, panel to panel (with the last one being DEL, and down to next page.).");
    }
    void HandleQueryScroll(int delta)
    {
        if (moduleSolved || !interactable) return;
        curQueryIdx = Mathf.Min(Mathf.Max(0, curQueryIdx + delta), allQueries.Count);
        UpdateQueryDisplay(curQueryIdx, forceQueryRevealIdx.Contains(curQueryIdx));
    }
    void HandleLetterScroll(int delta)
    {
        if (moduleSolved || !interactable) return;
        curPanelIDx = (curPanelIDx + delta) % 3;
        StopCoroutine(squareBtnAnim);
        squareBtnAnim = HandleDisplaySqAnim();
        StartCoroutine(squareBtnAnim);
    }
    void HandleLetterPress(int idx)
    {
        if (moduleSolved || !interactable) return;
        var expectedIDxPress = idx + 9 * curPanelIDx;
        if (expectedIDxPress >= alphabet.Length)
        {
            curWord = curWord.Length > 1 ? curWord.Substring(0, curWord.Length - 1) : "";
            if (curEncryptionsLetter.Any())
                curEncryptionsLetter.RemoveAt(curEncryptionsLetter.Count - 1);
        }
        else if (curWord.Length < 5)
        {
            curWord += alphabet[alphabetOrdering[expectedIDxPress]];
            curEncryptionsLetter.Add(selectedEncryptionTypes[expectedIDxPress]);
        }
        curQueryIdx = allQueries.Count;
        UpdateQueryDisplay(-1, false);
    }
    void HandleReset()
    {
        GenerateScramble();
        StopCoroutine(squareBtnAnim);
        squareBtnAnim = HandleDisplaySqAnim();
        StartCoroutine(squareBtnAnim);
        curWord = "";
        curEncryptionsLetter.Clear();
        UpdateQueryDisplay(-1);
    }

    void HandleQuery()
    {
        if (moduleSolved || !interactable) return;
        var requireStrike = false;
        if (curWord.Length == 0)
        {
            requireStrike = true;
            QuickLog("You queried literally nothing. Why!? I guess do you want a funny message to go here when you strike on this?");
        }
        if (curWord.Length < 5)
        {
            requireStrike = true;
            QuickLog("You queried a word that was not exactly 5 letters long: {0}", curWord);
        }
        else if (!Data.ObscureWordList.Contains(curWord) && !Data.GenerousWordList.Contains(curWord))
        {
            requireStrike = true;
            QuickLog("You queried a word that is not valid: {0}", curWord);
        }
        if (requireStrike)
        {
            modSelf.HandleStrike();
            HandleReset();
            return;
        }
        UpdateQueryDisplay(-1);
        QuickLog("Successfully queried this valid word: {0}", curWord);
        var responseCreated = new QueryResponse();
        responseCreated.wordQueried = curWord;
        responseCreated.encryptionTypes = curEncryptionsLetter.ToArray();
        var unmergedResponses = new List<PossibleResponse[]>();
        var wordQueried = selectedQuirk == PossibleQuirks.Symble ? correctWords.Single() : curWord;
        var wordsCorrect = selectedQuirk == PossibleQuirks.Symble ? new List<string> { curWord } : correctWords;
        for (var x = 0; x < wordsCorrect.Count; x++)
        {
            if (blacklistIdxesCorrect.Contains(x)) continue;
            var selectedCurCorrectWord = wordsCorrect[x];
            var distinctLettersInCorrectWord = selectedCurCorrectWord.Distinct();
            //QuickLogDebug(distinctLettersInCorrectWord.Join());
            var responseOfCurWord = new PossibleResponse[5];
            foreach (var AChar in distinctLettersInCorrectWord)
            {
                var idxesLettersInCorrectWord = Enumerable.Range(0, 5).Where(a => AChar == selectedCurCorrectWord[a]);
                var idxesLettersInCurrentWord = Enumerable.Range(0, 5).Where(a => AChar == wordQueried[a]);
                var idxesMatchBoth = idxesLettersInCurrentWord.Intersect(idxesLettersInCorrectWord);
                foreach (int xIdx in idxesMatchBoth)
                    responseOfCurWord[xIdx] = PossibleResponse.Correct;
                var idxesExceptInCorrect = idxesLettersInCurrentWord.Except(idxesLettersInCorrectWord).Take(idxesLettersInCorrectWord.Count() - idxesMatchBoth.Count());
                foreach (int xIdx in idxesExceptInCorrect)
                    responseOfCurWord[xIdx] = PossibleResponse.Almost;
            }
            unmergedResponses.Add(responseOfCurWord.ToArray());
            QuickLog("When comparing the guess \"{0}\" with the answer \"{1}\", the response given is {2}", wordQueried, selectedCurCorrectWord, responseOfCurWord.Select(a => a.ToString()).Join(", "));
        }
        var resultToDisplay = unmergedResponses.First();
        var allPossibleResponses = new[] { PossibleResponse.Absent, PossibleResponse.Correct, PossibleResponse.Almost };
        switch (selectedQuirk)
        {
            case PossibleQuirks.Symble:
            case PossibleQuirks.None:
                responseCreated.actualResponse = resultToDisplay;
                responseCreated.displayResponse = resultToDisplay;
                break;
            case PossibleQuirks.FiveOhOh:
                responseCreated.actualResponse = resultToDisplay;
                responseCreated.displayResponse = resultToDisplay.OrderBy(a => a == PossibleResponse.Correct ? 0 : a == PossibleResponse.Almost ? 1 : 2);
                break;
            case PossibleQuirks.Fibble:
                responseCreated.actualResponse = resultToDisplay;
                var idxTamper = Enumerable.Range(0, 5).PickRandom();
                var fakeResponse = allPossibleResponses.Where(b => b != resultToDisplay.ElementAt(idxTamper)).PickRandom();
                responseCreated.displayResponse = Enumerable.Range(0, 5).Select(a => a == idxTamper ? fakeResponse : resultToDisplay.ElementAt(a));
                break;
            case PossibleQuirks.Xordle:
                var finalResponse = resultToDisplay.ToArray();
                for (var x = 0; x < finalResponse.Length; x++)
                {
                    finalResponse[x] = finalResponse[x] == PossibleResponse.Absent ? unmergedResponses.Last().ElementAt(x) : finalResponse[x];
                }
                responseCreated.actualResponse = finalResponse;
                responseCreated.displayResponse = finalResponse;
                break;
        }
        QuickLog("The result being displayed with the active quirk is {0}", responseCreated.displayResponse.Select(a => a.ToString()).Join(", "));
        allQueries.Add(responseCreated);
        interactable = false;
        StartCoroutine(HandleQueryAnim(responseCreated));
    }
    void GenerateScramble(bool firstScramble = false)
    {
        curPanelIDx = 0;
        alphabetOrdering = Enumerable.Range(0, 26).ToArray().Shuffle();
        for (var x = 0; x < selectedEncryptionTypes.Length; x++)
        {
            var y = x;
            var filteredPossibleEncryptions = firstScramble ? allPossibleEncryptions : allPossibleEncryptions.Where(a => a != selectedEncryptionTypes[y]);
            selectedEncryptionTypes[x] = filteredPossibleEncryptions.PickRandom();
        }
        QuickLogDebug("Displaying the alphabet in reading order of the 3 panels: {0}" ,alphabetOrdering.Select(a => alphabet[a]).Join(""));
        QuickLogDebug("Displaying the encryptions in reading order of the 3 panels: {0}", selectedEncryptionTypes.Select(a => a.ToString()).Join(", "));
        //Debug.Log("FAULTYENCRYPTIONLINGO".ToCharArray().Shuffle().Join(""));
    }
    IEnumerator HandleQueryAnim(QueryResponse givenResponse)
    {
        var soundNames = new[] { "None", "Correct", "Close" };
        if (selectedQuirk == PossibleQuirks.Symble)
            soundNames.Shuffle();
        for (var x = 0; x < QueryResultRenderers.Length; x++)
        {
            var oneDisplayedResponseIdx = (int)givenResponse.displayResponse.ElementAt(x);
            QueryResultRenderers[x].material = possibleColors[queryColorIdxes[oneDisplayedResponseIdx]];
            colorblindLEDText[x].text = colorblindDetected ? possibleColorNames[queryColorIdxes[oneDisplayedResponseIdx]] : "";
            mAudio.PlaySoundAtTransform(soundNames[oneDisplayedResponseIdx], transform);
            yield return new WaitForSeconds(0.2f);
        }
        if (correctWords.Contains(givenResponse.wordQueried))
        {
            var idxToVoid = correctWords.IndexOf(givenResponse.wordQueried);
            if (selectedQuirk == PossibleQuirks.Fibble)
            {
                var idxGuarenteedLying = Enumerable.Range(0, 5).Where(a => givenResponse.displayResponse.ElementAt(a) != PossibleResponse.Correct);
                for (var x = 0; x < idxGuarenteedLying.Count(); x++)
                {
                    QueryResultRenderers[idxGuarenteedLying.ElementAt(x)].material = possibleColors[queryColorIdxes[1]];
                    colorblindLEDText[x].text = colorblindDetected ? possibleColorNames[queryColorIdxes[1]] : "";
                    mAudio.PlaySoundAtTransform("Correct", transform);
                }
                yield return new WaitForSeconds(0.2f);
                for (var t = 0; t < 2; t++)
                {
                    for (var x = 0; x < idxGuarenteedLying.Count(); x++)
                    {
                        var randomZeroOrTwo = new[] { 0, 2 }.PickRandom();
                        QueryResultRenderers[idxGuarenteedLying.ElementAt(x)].material = possibleColors[queryColorIdxes[randomZeroOrTwo]];
                        colorblindLEDText[x].text = colorblindDetected ? possibleColorNames[queryColorIdxes[randomZeroOrTwo]] : "";
                        mAudio.PlaySoundAtTransform(soundNames[randomZeroOrTwo], transform);
                    }
                    yield return new WaitForSeconds(0.2f);
                    for (var x = 0; x < idxGuarenteedLying.Count(); x++)
                    {
                        QueryResultRenderers[idxGuarenteedLying.ElementAt(x)].material = possibleColors[queryColorIdxes[1]];
                        colorblindLEDText[x].text = colorblindDetected ? possibleColorNames[queryColorIdxes[1]] : "";
                        mAudio.PlaySoundAtTransform("Correct", transform);
                    }
                    yield return new WaitForSeconds(0.2f);
                }

            }
            if (!blacklistIdxesCorrect.Contains(idxToVoid))
            {
                blacklistIdxesCorrect.Add(idxToVoid);
                moduleSolved = blacklistIdxesCorrect.Count >= correctWords.Count;
                mAudio.PlaySoundAtTransform("Solve", transform);
                for (var x = 0; x < QueryImgRenderers.Length; x++)
                {
                    QueryImgRenderers[x].enabled = false;
                    QueryResultRenderers[x].material = inactiveColor;
                    colorblindLEDText[x].text = "";
                    yield return new WaitForSeconds(0.025f);
                }
                for (var x = 0; x < ScreenText.Length; x++)
                {
                    ScreenText[x].text = x < givenResponse.wordQueried.Length ? givenResponse.wordQueried[x].ToString() : "";
                    QueryResultRenderers[x].material = possibleColors[1];
                    colorblindLEDText[x].text = colorblindDetected ? possibleColorNames[1] : "";
                    yield return new WaitForSeconds(0.025f);
                }
                forceQueryRevealIdx.Add(allQueries.Count - 1);
                if (moduleSolved)
                {
                    QuickLog("You revealed all of the words. You showed that module who's the boss now.");
                    modSelf.HandlePass();
                    for (var x = 0; x < SqButtonDisplayRenderers.Length; x++)
                    {
                        SqButtonDisplayRenderers[x].enabled = false;
                        yield return new WaitForSeconds(0.025f);
                    }
                    yield break;
                }
                QuickLog("You revealed a word. {0} to go.", correctWords.Count - blacklistIdxesCorrect.Count);
            }
        }
        yield return new WaitForSeconds(1.5f);
        HandleReset();
        interactable = true;
        yield break;
    }
    IEnumerator HandleDisplaySqAnim()
    {
        for (var x = 0; x < SqButtonDisplayRenderers.Length; x++)
        {
            SqButtonDisplayRenderers[x].enabled = false;
            yield return new WaitForSeconds(0.025f);
        }
        var selectedIdxesShow = Enumerable.Range(9 * curPanelIDx, 9);
        for (var x = 0; x < selectedIdxesShow.Count(); x++)
        {
            var curIdx = selectedIdxesShow.ElementAt(x);
            if (curIdx >= alphabetOrdering.Length)
            {
                SqButtonDisplayRenderers[x].transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                SqButtonDisplayRenderers[x].material.mainTexture = deleteTexture;
            }
            else
            {
                var curEncryptionType = selectedEncryptionTypes[curIdx];
                SqButtonDisplayRenderers[x].transform.localScale = possibleLocalScaleModifiers.ContainsKey(curEncryptionType) ? possibleLocalScaleModifiers[curEncryptionType] : new Vector3(0.125f, 0.125f, 0.1f);
                switch (curEncryptionType)
                {
                    case PossibleEncryptions.BoozleSet1:
                        SqButtonDisplayRenderers[x].material.mainTexture = BoozleglyphTextures1[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.BoozleSet2:
                        SqButtonDisplayRenderers[x].material.mainTexture = BoozleglyphTextures2[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.BoozleSet3:
                        SqButtonDisplayRenderers[x].material.mainTexture = BoozleglyphTextures3[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.Braille:
                        SqButtonDisplayRenderers[x].material.mainTexture = BrailleTextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.Maritime:
                        SqButtonDisplayRenderers[x].material.mainTexture = MaritimeTextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.Pigpen:
                        SqButtonDisplayRenderers[x].material.mainTexture = PigpenTextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.Lombax:
                        SqButtonDisplayRenderers[x].material.mainTexture = LombaxTextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.StandardGalacticAlphabet:
                        SqButtonDisplayRenderers[x].material.mainTexture = SGATextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.Zoni:
                        SqButtonDisplayRenderers[x].material.mainTexture = ZoniTextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.SignLanguage:
                        SqButtonDisplayRenderers[x].material.mainTexture = SignLanguageTextures[alphabetOrdering[curIdx]];
                        break;
                    case PossibleEncryptions.Semaphore:
                        SqButtonDisplayRenderers[x].material.mainTexture = SemaphoreTextures[alphabetOrdering[curIdx]];
                        break;
                }
            }
        }
        for (var x = 0; x < SqButtonDisplayRenderers.Length; x++)
        {
            SqButtonDisplayRenderers[x].enabled = true;
            yield return new WaitForSeconds(0.025f);
        }
    }

    void UpdateQueryDisplay(int idx, bool revealed = false)
    {
        if (idx < 0 || idx >= allQueries.Count)
        {
            for (var x = 0; x < QueryResultRenderers.Length; x++)
            {
                QueryResultRenderers[x].material = inactiveColor;
                colorblindLEDText[x].text = "";
            }
            if (revealed)
            {
                for (var x = 0; x < ScreenText.Length; x++)
                {
                    QueryImgRenderers[x].enabled = false;
                    ScreenText[x].text = x < curWord.Length ? curWord[x].ToString() : "";
                }
                return;
            }
            for (var x = 0; x < QueryImgRenderers.Length; x++)
            {
                QueryImgRenderers[x].enabled = x < curEncryptionsLetter.Count;
                ScreenText[x].text = "";
                if (x < curEncryptionsLetter.Count)
                {
                    var curEncryptionType = curEncryptionsLetter[x];
                    var curLetter = alphabet.IndexOf(curWord[x]);
                    QueryImgRenderers[x].transform.localScale = possibleLocalScaleModifiers.ContainsKey(curEncryptionType) ? possibleLocalScaleModifiers[curEncryptionType] : new Vector3(0.125f, 0.125f, 0.1f);
                    switch (curEncryptionType)
                    {
                        case PossibleEncryptions.BoozleSet1:
                            QueryImgRenderers[x].material.mainTexture = BoozleglyphTextures1[curLetter];
                            break;
                        case PossibleEncryptions.BoozleSet2:
                            QueryImgRenderers[x].material.mainTexture = BoozleglyphTextures2[curLetter];
                            break;
                        case PossibleEncryptions.BoozleSet3:
                            QueryImgRenderers[x].material.mainTexture = BoozleglyphTextures3[curLetter];
                            break;
                        case PossibleEncryptions.Braille:
                            QueryImgRenderers[x].material.mainTexture = BrailleTextures[curLetter];
                            break;
                        case PossibleEncryptions.Maritime:
                            QueryImgRenderers[x].material.mainTexture = MaritimeTextures[curLetter];
                            break;
                        case PossibleEncryptions.Pigpen:
                            QueryImgRenderers[x].material.mainTexture = PigpenTextures[curLetter];
                            break;
                        case PossibleEncryptions.Lombax:
                            QueryImgRenderers[x].material.mainTexture = LombaxTextures[curLetter];
                            break;
                        case PossibleEncryptions.StandardGalacticAlphabet:
                            QueryImgRenderers[x].material.mainTexture = SGATextures[curLetter];
                            break;
                        case PossibleEncryptions.Zoni:
                            QueryImgRenderers[x].material.mainTexture = ZoniTextures[curLetter];
                            break;
                        case PossibleEncryptions.SignLanguage:
                            QueryImgRenderers[x].material.mainTexture = SignLanguageTextures[curLetter];
                            break;
                        case PossibleEncryptions.Semaphore:
                            QueryImgRenderers[x].material.mainTexture = SemaphoreTextures[curLetter];
                            break;
                    }
                }
            }
            return;
        }
        var selectedQuery = allQueries[idx];
        for (var x = 0; x < QueryResultRenderers.Length; x++)
        {
            var oneDisplayedResponseIdx = (int)selectedQuery.displayResponse.ElementAt(x);
            QueryResultRenderers[x].material = possibleColors[queryColorIdxes[oneDisplayedResponseIdx]];
            colorblindLEDText[x].text = colorblindDetected ? possibleColorNames[queryColorIdxes[oneDisplayedResponseIdx]] : "";
        }
        if (revealed)
        {
            for (var x = 0; x < ScreenText.Length; x++)
            {
                QueryImgRenderers[x].enabled = false;
                ScreenText[x].text = x < selectedQuery.wordQueried.Length ? selectedQuery.wordQueried[x].ToString() : "";
            }
            return;
        }
        for (var x = 0; x < QueryImgRenderers.Length; x++)
        {
            ScreenText[x].text = "";
            QueryImgRenderers[x].enabled = x < selectedQuery.wordQueried.Length;
            if (x < selectedQuery.wordQueried.Length)
            {
                var curEncryptionType = selectedQuery.encryptionTypes.ElementAt(x);
                var curLetter = alphabet.IndexOf(selectedQuery.wordQueried[x]);
                QueryImgRenderers[x].transform.localScale = possibleLocalScaleModifiers.ContainsKey(curEncryptionType) ? possibleLocalScaleModifiers[curEncryptionType] : new Vector3(0.125f, 0.125f, 0.1f);
                switch (curEncryptionType)
                {
                    case PossibleEncryptions.BoozleSet1:
                        QueryImgRenderers[x].material.mainTexture = BoozleglyphTextures1[curLetter];
                        break;
                    case PossibleEncryptions.BoozleSet2:
                        QueryImgRenderers[x].material.mainTexture = BoozleglyphTextures2[curLetter];
                        break;
                    case PossibleEncryptions.BoozleSet3:
                        QueryImgRenderers[x].material.mainTexture = BoozleglyphTextures3[curLetter];
                        break;
                    case PossibleEncryptions.Braille:
                        QueryImgRenderers[x].material.mainTexture = BrailleTextures[curLetter];
                        break;
                    case PossibleEncryptions.Maritime:
                        QueryImgRenderers[x].material.mainTexture = MaritimeTextures[curLetter];
                        break;
                    case PossibleEncryptions.Pigpen:
                        QueryImgRenderers[x].material.mainTexture = PigpenTextures[curLetter];
                        break;
                    case PossibleEncryptions.Lombax:
                        QueryImgRenderers[x].material.mainTexture = LombaxTextures[curLetter];
                        break;
                    case PossibleEncryptions.StandardGalacticAlphabet:
                        QueryImgRenderers[x].material.mainTexture = SGATextures[curLetter];
                        break;
                    case PossibleEncryptions.Zoni:
                        QueryImgRenderers[x].material.mainTexture = ZoniTextures[curLetter];
                        break;
                    case PossibleEncryptions.SignLanguage:
                        QueryImgRenderers[x].material.mainTexture = SignLanguageTextures[curLetter];
                        break;
                    case PossibleEncryptions.Semaphore:
                        QueryImgRenderers[x].material.mainTexture = SemaphoreTextures[curLetter];
                        break;
                }
            }
        }
    }
}
