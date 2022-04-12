using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class EncryptionLingoScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable[] SqButtonSels;
    public KMSelectable[] LetterArrowSels;
    public KMSelectable[] PastSubArrowSels;
    public KMSelectable QuerySel;
    public GameObject[] SqButtonObjs;
    public GameObject[] QueryStateLeds;
    public Material[] QueryMats;
    public GameObject[] ButtonImages;
    public Texture DeleteTexture;
    public GameObject[] QueryImages;
    public TextMesh[] ScreenText;
    public GameObject[] ScreenTextObjs;

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

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;
    private int _queryIx = 0;
    private List<string> _pastQueries = new List<string>();
    private List<EncryptionMethods> _pastEncryptions = new List<EncryptionMethods>();
    private List<QueryState[]> _pastQueryStates = new List<QueryState[]>();
    private List<int> _pastBoozleSets = new List<int>();
    private string _correctWord;
    private Coroutine _animation;
    private enum EncryptionMethods
    {
        Maritime,
        Braille,
        Boozleglyph,
        Pigpen,
        SignLanguage,
        Semaphore,
        Zoni,
        Lombax,
        SGA
    };
    private enum QueryState
    {
        None,
        Correct,
        Close
    }
    private EncryptionMethods?[] _encChecker = new EncryptionMethods?[2];
    private int _currentPage;
    private bool _isQueryAnimating;
    private bool _startup = true;
    private EncryptionMethods _currentEncryption;
    private int[] _letterOrder = new int[26];
    private int _boozleSet;
    private string _currentInput = "";

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < SqButtonSels.Length; i++)
            SqButtonSels[i].OnInteract += SqButtonPress(i);
        for (int i = 0; i < LetterArrowSels.Length; i++)
            LetterArrowSels[i].OnInteract += LetterArrowPress(i);
        for (int i = 0; i < PastSubArrowSels.Length; i++)
            PastSubArrowSels[i].OnInteract += PastSubArrowPress(i);
        QuerySel.OnInteract += QueryPress;

        _correctWord = Data.GenerousWordList.PickRandom();
        Debug.LogFormat("[Encryption Lingo #{0}] Chosen word: {1}", _moduleId, _correctWord);
        for (int i = 0; i < _correctWord.Length; i++)
        {
            ScreenText[i].text = _correctWord.Substring(i, 1);
            ScreenTextObjs[i].SetActive(false);
        }
        Module.OnActivate += delegate () { _animation = StartCoroutine(SetButtons()); };
        SetEncryptions();
        foreach (var img in QueryImages)
            img.SetActive(false);
        foreach (var img in ButtonImages)
            img.SetActive(false);
    }

    private bool QueryPress()
    {
        QuerySel.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        if (!_moduleSolved && !_isQueryAnimating)
        {
            _queryIx = _pastQueries.Count;
            SetScreen(_currentInput, _currentEncryption, false);
            if (_currentInput.Length != 5)
            {
                Debug.LogFormat("[Encryption Lingo #{0}] Queried {1}. Strike.", _moduleId, _currentInput.Length == 0 ? "nothing" : _currentInput + ", which is not a five-letter word");
                SetEncryptions();
                _animation = StartCoroutine(SetButtons(true));
                Module.HandleStrike();
                return false;
            }
            if (!Data.ObscureWordList.Contains(_currentInput) && !Data.GenerousWordList.Contains(_currentInput))
            {
                Debug.LogFormat("[Encryption Lingo #{0}] Queried {1}, which is not a valid word. Strike.", _moduleId, _currentInput);
                SetEncryptions();
                _animation = StartCoroutine(SetButtons(true));
                Module.HandleStrike();
                return false;
            }
            _pastQueries.Add(_currentInput);
            _pastEncryptions.Add(_currentEncryption);
            _pastQueryStates.Add(QueryWord(_currentInput));
            _pastBoozleSets.Add(_boozleSet);
            StartCoroutine(AnimateCurrentQuery(_currentInput, QueryWord(_currentInput)));
        }
        return false;
    }

    private IEnumerator AnimateCurrentQuery(string word, QueryState[] qry)
    {
        _isQueryAnimating = true;
        _queryIx = _pastQueries.Count;
        SetScreen(_currentInput, _currentEncryption, false);
        var corCount = 0;
        var qryLights = "";
        var soundNames = new string[] { "None", "Correct", "Close" };
        for (int i = 0; i < 5; i++)
            qryLights += qry[i] == QueryState.None ? "R" : qry[i] == QueryState.Correct ? "G" : "Y";
        Debug.LogFormat("[Encryption Lingo #{0}] Queried {1} with result {2}.", _moduleId, word, qryLights);
        for (int i = 0; i < 5; i++)
        {
            QueryStateLeds[i].GetComponent<MeshRenderer>().sharedMaterial = QueryMats[(int)qry[i]];
            if ((int)qry[i] == 1)
                corCount++;
            Audio.PlaySoundAtTransform(soundNames[(int)qry[i]], transform);
            yield return new WaitForSeconds(0.2f);
        }
        if (corCount == 5)
        {
            _moduleSolved = true;
            Audio.PlaySoundAtTransform("Solve", transform);
            Debug.LogFormat("[Encryption Lingo #{0}] You guessed the correct word, {1}. Module solved!", _moduleId, _correctWord);
            for (int i = 0; i < 5; i++)
            {
                QueryImages[i].SetActive(false);
                yield return new WaitForSeconds(0.025f);
            }
            for (int i = 0; i < 5; i++)
            {
                ScreenTextObjs[i].SetActive(true);
                yield return new WaitForSeconds(0.025f);
            }
            Module.HandlePass();
            yield break;
        }
        yield return new WaitForSeconds(1.5f);
        for (int i = 0; i < 5; i++)
            QueryStateLeds[i].GetComponent<MeshRenderer>().sharedMaterial = QueryMats[3];
        _currentInput = "";
        foreach (var img in QueryImages)
            img.SetActive(false);
        SetEncryptions();
        _animation = StartCoroutine(SetButtons());
        _isQueryAnimating = false;
        yield break;
    }

    private QueryState[] QueryWord(string word)
    {
        var solList = _correctWord.ToList();
        var states = new QueryState[5];
        for (int i = 0; i < 5; i++)
        {
            if (word[i] == _correctWord[i])
            {
                states[i] = QueryState.Correct;
                solList.Remove(word[i]);
            }
        }
        for (int i = 0; i < 5; i++)
        {
            if (states[i] != QueryState.Correct && solList.Contains(word[i]))
            {
                states[i] = QueryState.Close;
                solList.Remove(word[i]);
            }
        }
        return states;
    }

    private KMSelectable.OnInteractHandler PastSubArrowPress(int btn)
    {
        return delegate ()
        {
            PastSubArrowSels[btn].AddInteractionPunch(0.5f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            if (!_moduleSolved && !_isQueryAnimating)
            {
                if (btn == 0)
                {
                    if (_queryIx > 0)
                    {
                        _queryIx--;
                        SetScreen(_pastQueries[_queryIx], _pastEncryptions[_queryIx], true, _pastQueryStates[_queryIx]);
                    }
                }
                if (btn == 1)
                {
                    if (_queryIx < _pastQueries.Count)
                    {
                        _queryIx++;
                        if (_queryIx < _pastQueries.Count)
                            SetScreen(_pastQueries[_queryIx], _pastEncryptions[_queryIx], true, _pastQueryStates[_queryIx]);
                        else
                            SetScreen(_currentInput, _currentEncryption, true);
                    }
                }
            }
            return false;
        };
    }

    private KMSelectable.OnInteractHandler SqButtonPress(int btn)
    {
        return delegate ()
        {
            SqButtonSels[btn].AddInteractionPunch(0.5f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            if (!_moduleSolved && !_isQueryAnimating)
                SetInput(btn);
            return false;
        };
    }

    private void SetInput(int btn)
    {
        _queryIx = _pastQueryStates.Count;
        SetScreen(_currentInput, _currentEncryption, false);
        if ((btn + _currentPage * 9) == 26)
        {
            if (_currentInput.Length >= 1)
                _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
            for (int i = 0; i < 5; i++)
                if (_currentInput.Length <= i)
                    QueryImages[i].SetActive(false);
        }
        else if (_currentInput.Length != 5)
        {
            _currentInput += "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Substring(_letterOrder[btn + _currentPage * 9], 1);
            SetScreen(_currentInput, _currentEncryption, false);
        }
        // Debug.LogFormat("[Encryption Lingo #{0}] Current input: {1}", _moduleId, _currentInput);
    }

    private void SetScreen(string input, EncryptionMethods enc, bool isPast, QueryState[] qry = null)
    {
        for (int i = 0; i < QueryStateLeds.Length; i++)
            QueryStateLeds[i].GetComponent<MeshRenderer>().sharedMaterial = QueryMats[qry == null ? 3 : (int)qry[i]];
        for (int i = 0; i < 5; i++)
        {
            if (input.Length <= i)
            {
                QueryImages[i].SetActive(false);
                continue;
            }
            QueryImages[i].SetActive(true);
            var c = input[i] - 'A';
            if (enc == EncryptionMethods.Maritime)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = MaritimeTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.Braille)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BrailleTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.085f, 0.125f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.Boozleglyph)
            {
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                if (isPast && _queryIx != _pastQueries.Count)
                {
                    if (_pastBoozleSets[_queryIx] == 0)
                        QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures1[c];
                    if (_pastBoozleSets[_queryIx] == 1)
                        QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures2[c];
                    if (_pastBoozleSets[_queryIx] == 2)
                        QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures3[c];
                    continue;
                }
                else
                {
                    if (_boozleSet == 0)
                        QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures1[c];
                    if (_boozleSet == 1)
                        QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures2[c];
                    if (_boozleSet == 2)
                        QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures3[c];
                    continue;
                }
            }
            if (enc == EncryptionMethods.Pigpen)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = PigpenTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.SignLanguage)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = SignLanguageTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.175f, 0.175f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.Semaphore)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = SemaphoreTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.15f, 0.12f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.Zoni)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = ZoniTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.175f, 0.175f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.Lombax)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = LombaxTextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
                continue;
            }
            if (enc == EncryptionMethods.SGA)
            {
                QueryImages[i].GetComponent<MeshRenderer>().material.mainTexture = SGATextures[c];
                foreach (var img in QueryImages)
                    img.transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
                continue;
            }
        }
    }

    private KMSelectable.OnInteractHandler LetterArrowPress(int btn)
    {
        return delegate ()
        {
            LetterArrowSels[btn].AddInteractionPunch(0.5f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            if (!_moduleSolved && !_isQueryAnimating)
            {
                if (btn == 0)
                    _currentPage = (_currentPage + 1) % 3;
                if (btn == 1)
                    _currentPage = (_currentPage + 2) % 3;
                if (_animation != null)
                    StopCoroutine(_animation);
                _animation = StartCoroutine(SetButtons());
            }
            return false;
        };
    }

    private void SetEncryptions()
    {
        _letterOrder = Enumerable.Range(0, 26).ToArray().Shuffle();
        pickEnc:
        _currentEncryption = (EncryptionMethods)Rnd.Range(0, Enum.GetValues(typeof(EncryptionMethods)).Length);
        if (_encChecker.Contains(_currentEncryption))
            goto pickEnc;
        _encChecker[1] = _encChecker[0];
        _encChecker[0] = _currentEncryption;
        if (_currentEncryption == EncryptionMethods.Maritime)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Maritime Flags", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.Braille)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Braille", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.085f, 0.125f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.Boozleglyph)
        {
            _boozleSet = Rnd.Range(0, 3);
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Boozleglyph Set {1}", _moduleId, _boozleSet + 1);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.Pigpen)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Pigpen", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.SignLanguage)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Sign Language", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.175f, 0.175f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.Semaphore)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Semaphore", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.15f, 0.12f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.Zoni)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Zoni", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.175f, 0.175f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.Lombax)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: Lombax", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
        }
        else if (_currentEncryption == EncryptionMethods.SGA)
        {
            Debug.LogFormat("[Encryption Lingo #{0}] Chosen encryption method: SGA", _moduleId);
            foreach (var img in QueryImages)
                img.transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
        }
    }

    private IEnumerator SetButtons(bool isStriking = false)
    {
        if (isStriking)
        {
            _currentInput = "";
            foreach (var img in QueryImages)
                img.SetActive(false);
        }
        if (!_startup)
        {
            for (int i = 0; i < 9; i++)
            {
                ButtonImages[i].SetActive(false);
                yield return new WaitForSeconds(0.025f);
            }
        }
        _startup = false;
        for (int i = _currentPage * 9; i < (_currentPage * 9 + 9); i++)
        {
            if (i == 26)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = DeleteTexture;
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Maritime)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = MaritimeTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Braille)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.085f, 0.125f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = BrailleTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Boozleglyph)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                if (_boozleSet == 0)
                    ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures1[_letterOrder[i]];
                else if (_boozleSet == 1)
                    ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures2[_letterOrder[i]];
                else
                    ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = BoozleglyphTextures3[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Pigpen)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.125f, 0.125f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = PigpenTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.SignLanguage)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.175f, 0.175f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = SignLanguageTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Semaphore)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.15f, 0.12f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = SemaphoreTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Zoni)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.175f, 0.175f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = ZoniTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.Lombax)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = LombaxTextures[_letterOrder[i]];
                continue;
            }
            if (_currentEncryption == EncryptionMethods.SGA)
            {
                ButtonImages[i % 9].transform.localScale = new Vector3(0.15f, 0.15f, 0.1f);
                ButtonImages[i % 9].GetComponent<MeshRenderer>().material.mainTexture = SGATextures[_letterOrder[i]];
                continue;
            }
        }
        for (int i = 0; i < 9; i++)
        {
            ButtonImages[i].SetActive(true);
            yield return new WaitForSeconds(0.025f);
        }
        _animation = null;
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "!{0} A1 B2 C3 [Press buttons in A1/B2/C3] | !{0} query [Query the current word] | !{0} up/down [Scroll through the pages] | !{0} left/right [Scroll through previous queries]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        // Twitch Plays support implemented by Timwi.
        var commandPieces = command.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var buttonsToPress = new List<KMSelectable>();
        Match m;

        foreach (var piece in commandPieces)
        {
            if (Regex.IsMatch(piece, @"^\s*q(?:uery)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                buttonsToPress.Add(QuerySel);

            else if ((m = Regex.Match(piece, @"^\s*(?:up?|(?<d>d(?:own)?))\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
                buttonsToPress.Add(LetterArrowSels[m.Groups["d"].Success ? 1 : 0]);

            else if ((m = Regex.Match(piece, @"^\s*(?:l(?:eft)?|(?<r>r(?:ight)?))\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
                buttonsToPress.Add(PastSubArrowSels[m.Groups["r"].Success ? 1 : 0]);

            else if ((m = Regex.Match(piece, @"^\s*(?<x>[A-C])(?<y>[1-3])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
                buttonsToPress.Add(SqButtonSels[m.Groups["x"].Value.ToUpperInvariant()[0] - 'A' + 3 * (m.Groups["y"].Value[0] - '1')]);

            else
                yield break;
        }
        yield return null;

        foreach (var btn in buttonsToPress)
        {
            btn.OnInteract();
            yield return new WaitForSeconds(.1f);
            while (_isQueryAnimating)
                yield return null;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        // Autosolver implemented by Timwi.
        while (_animation != null)
            yield return true;

        var ix = 0;
        while (ix < _currentInput.Length && _currentInput[ix] == _correctWord[ix])
            ix++;

        while (_currentInput.Length > ix)
        {
            while (_currentPage != 2)
            {
                LetterArrowSels[_currentPage ^ 1].OnInteract();
                while (_animation != null)
                    yield return true;
            }
            SqButtonSels[8].OnInteract();
            yield return new WaitForSeconds(.1f);
        }

        while (_currentInput.Length < _correctWord.Length)
        {
            var correctLetterIx = Array.IndexOf(_letterOrder, _correctWord[_currentInput.Length] - 'A');
            var correctPage = correctLetterIx / 9;
            if (correctPage != _currentPage)
            {
                LetterArrowSels[((_currentPage - correctPage + 3) % 3) % 2].OnInteract();       // don’t ask
                while (_animation != null)
                    yield return true;
            }
            SqButtonSels[correctLetterIx - 9 * correctPage].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
        QuerySel.OnInteract();
        while (!_moduleSolved || _isQueryAnimating)
            yield return true;
    }
}
