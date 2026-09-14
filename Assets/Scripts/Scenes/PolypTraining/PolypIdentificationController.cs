// ============================================================================
// PolypIdentificationController.cs
//
// Manages the polyp identification sub-phase: the trainee classifies each
// detected polyp's characteristics (location, Paris class, JNET class, size)
// before training resumes.
//
// Responsibilities:
//   - Maintains the ordered queue of polyps awaiting identification.
//   - Provides classification dropdown option lists to the UI.
//   - Validates trainee answers against ground-truth polyp data.
//   - Computes per-polyp weighted scores and accumulates answer records.
//
// Identification lifecycle (called by PolypTrainingManager):
//   1. SetPolypsForIndentification() — loads detected polyps into queue.
//   2. SetUpNextPolypToIdentify()   — advances _currentPolypInIdentification.
//   3. GetSizeDropdownOptions()     — generates randomised size choices.
//   4. RegisterPolypIdentificationAnswer() — validates, scores, records answer.
//   5. EndPolypIdentification()     — clears ongoing flag when queue is empty.
//
// Per-polyp scoring (0–10):
//   score = (correctSize ? sizeW : 0) + (correctLocation ? locationW : 0)
//         + (correctParis ? parisW : 0) + (correctJnet ? jnetW : 0)   × 10
//   Default weights are equal (0.25 each). Weights must sum to 1.0.
//
// Size option generation:
//   GenerateRandomVector2FromValue() produces sizeOptionsCount distractors
//   within ±1 mm of the correct size, rounded to 0.5 mm steps. The correct
//   size is always included and the list is shuffled.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PolypTraining
{
    /// <summary>
    /// Manages the polyp identification sub-phase in Polyp Training.
    /// Handles the queue of detected polyps, provides dropdown options for the UI,
    /// validates trainee answers against ground truth, and scores each identification.
    /// </summary>
    /// <remarks>
    /// Owned and orchestrated by PolypTrainingManager.
    /// Suspends the training timer via _isPolypIdentificationOngoing while the queue is non-empty.
    /// </remarks>
    public class PolypIdentificationController : MonoBehaviour
    {

        [Header("Weights for final score calculation (total must be 1)")]
        [SerializeField] private float _sizeWeight = 0.25f;
        [SerializeField] private float _locationWeight = 0.25f;
        [SerializeField] private float _parisClassWeight = 0.25f;
        [SerializeField] private float _jnetClassWeight = 0.25f;
        [SerializeField] private float sizeOptionsPercentageDeviation = 0.25f;
        [SerializeField] private int sizeOptionsCount = 6;
        public List<PolypIdentificationAnswer> IdentificationAnswers { get; private set; }
        public int PolypsToIdentifyCount { get { return _polypsToIdentify.Count; } }
        public int IdentifiedPolypCount { get; private set; } = 0;

        public bool _isPolypIdentificationOngoing = false;
        private Polyp _currentPolypInIdentification;
        private List<Polyp> _polypsToIdentify;
        private const string BlankDropdownOption = "--";

        private void Start()
        {
            IdentificationAnswers = new List<PolypIdentificationAnswer>();
            _polypsToIdentify = new List<Polyp>();
        }

        /// <summary>
        /// Returns display strings for every intestine location enum value.
        /// Used to populate the location dropdown in the identification UI.
        /// </summary>
        public List<string> GetDiseaseLocationDropdownOptions()
        {
            var options = Enum.GetValues(typeof(Disease.IntestineLocation))
                .Cast<Disease.IntestineLocation>()
                .Select(Disease.GetLocationString).ToList();
            options.Insert(0, BlankDropdownOption);
            return options;
        }

        /// <summary>
        /// Returns display strings for every Paris morphology classification value.
        /// Used to populate the Paris classification dropdown in the identification UI.
        /// </summary>
        public List<string> GetParisClassDropdownOptions()
        {
            var options = Polyp.GetParisClassificationStrings().ToList();
            options.Insert(0, BlankDropdownOption);
            return options;
        }

        /// <summary>
        /// Returns display strings for every JNET neoplasia classification value.
        /// Used to populate the JNET classification dropdown in the identification UI.
        /// </summary>
        public List<string> GetJnetClassDropdownOptions()
        {
            var options = Polyp.GetJnetClassificationStrings().ToList();
            options.Insert(0, BlankDropdownOption);
            return options;
        }

        /// <summary>
        /// Returns a shuffled list of "WxH" size option strings for the current polyp's size dropdown.
        /// Exactly one entry matches the correct size; the rest are random distractors.
        /// </summary>
        /// <remarks>
        /// Must be called after SetUpNextPolypToIdentify() to ensure the correct polyp is active.
        /// Delegates to GenerateRandomVector2FromValue() for distractor generation.
        /// </remarks>
        public List<string> GetSizeDropdownOptions()
        {
            var sizeOptions = GenerateRandomVector2FromValue(_currentPolypInIdentification.Size, sizeOptionsCount - 1);
            var result = sizeOptions.Select(o => o.x + "x" + o.y).ToList();
            result.Insert(0, BlankDropdownOption);
            return result;
        }

        /// <summary>
        /// Returns the close-up reference image for the polyp currently being identified.
        /// Falls back to Texture2D.blackTexture if no image is assigned.
        /// </summary>
        public Texture GetPolypInIdentificationImage()
        {
            return _currentPolypInIdentification.Image ?? Texture2D.blackTexture;
        }

        /// <summary>
        /// Parses the trainee's answer snapshot, evaluates correctness, scores the answer, and records it.
        /// Returns the populated PolypIdentificationAnswer.
        /// </summary>
        /// <param name="snapshot">Raw UI selections: size string, location index, Paris class, JNET class.</param>
        /// <remarks>
        /// Processing steps:
        /// 1. Parse size string ("WxH") to Vector2.
        /// 2. Cast location index to Disease.IntestineLocation.
        /// 3. Resolve Paris / JNET strings to enum values via TryGet* helpers.
        /// 4. Compare each field to _currentPolypInIdentification.
        /// 5. Score via CalculateAnswerScore(), add to IdentificationAnswers, remove polyp from queue.
        /// </remarks>
        public PolypIdentificationAnswer RegisterPolypIdentificationAnswer(in PolypIdentificationSnapshot snapshot)
        {
            Vector2 selectedSize;
            bool isSizeCorrect;
            if (snapshot.SizeAnswer == BlankDropdownOption)
            {
                isSizeCorrect = false;
            }
            else
            {
                var parts = snapshot.SizeAnswer.Split("x");
                selectedSize = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                isSizeCorrect = selectedSize == _currentPolypInIdentification.Size;
            }

            // Index 0 is the blank option; real enum values start at index 1.
            Disease.IntestineLocation? selectedLocation = null;
            bool isLocationCorrect;
            if (snapshot.DiseaseLocationAnswer == 0)
            {
                isLocationCorrect = false;
            }
            else
            {
                selectedLocation = (Disease.IntestineLocation)(snapshot.DiseaseLocationAnswer - 1);
                isLocationCorrect = selectedLocation == _currentPolypInIdentification.Location;
            }

            Polyp.ParisClassification? selectedParisClass = null;
            bool isParisClassCorrect;
            if (snapshot.ParisClassAnswer == BlankDropdownOption)
            {
                isParisClassCorrect = false;
            }
            else
            {
                Polyp.TryGetParisClassification(snapshot.ParisClassAnswer, out var paris);
                selectedParisClass = paris;
                isParisClassCorrect = selectedParisClass == _currentPolypInIdentification.ParisClass;
            }

            Polyp.JNETClassification? selectedJnetClass = null;
            bool isJnetClassCorrect;
            if (snapshot.JnetClassAnswer == BlankDropdownOption)
            {
                isJnetClassCorrect = false;
            }
            else
            {
                Polyp.TryGetJnetClassification(snapshot.JnetClassAnswer, out var jnet);
                selectedJnetClass = jnet;
                isJnetClassCorrect = selectedJnetClass == _currentPolypInIdentification.JnetClass;
            }
            var polypIdentificationAnswer = new PolypIdentificationAnswer
            (
                _currentPolypInIdentification,
                snapshot.SizeAnswer == BlankDropdownOption ? string.Empty : snapshot.SizeAnswer,
                isSizeCorrect,
                selectedLocation,
                isLocationCorrect,
                selectedParisClass,
                isParisClassCorrect,
                selectedJnetClass,
                isJnetClassCorrect,
                CalculateAnswerScore(isSizeCorrect, isLocationCorrect, isParisClassCorrect, isJnetClassCorrect), 
                wasNotIdentified: false
            );

            IdentificationAnswers.Add(polypIdentificationAnswer);
            IdentifiedPolypCount++;
            _polypsToIdentify.Remove(_currentPolypInIdentification);

            return polypIdentificationAnswer;
        }

        /// <summary>
        /// Loads a new queue of polyps for identification and sets the ongoing flag.
        /// Called by PolypTrainingManager immediately after polyp detection.
        /// </summary>
        /// <param name="polypsToIdentify">Ordered list of newly detected polyps to classify.</param>
        public void SetPolypsForIndentification(List<Polyp> polypsToIdentify)
        {
            _polypsToIdentify = polypsToIdentify;
            _isPolypIdentificationOngoing = true;
        }

        /// <summary>
        /// Advances _currentPolypInIdentification to the first entry in the queue.
        /// Must be called before GetPolypInIdentificationImage() or GetSizeDropdownOptions().
        /// </summary>
        /// <remarks>
        /// Does not remove the polyp from the queue — removal happens in RegisterPolypIdentificationAnswer().
        /// </remarks>
        public void SetUpNextPolypToIdentify()
        {
            _currentPolypInIdentification = _polypsToIdentify[0];
        }

        /// <summary>
        /// Clears the identification-ongoing flag once all queued polyps have been identified.
        /// Called by PolypTrainingManager when the queue is empty.
        /// </summary>
        public void EndPolypIdentification()
        {
            _isPolypIdentificationOngoing = false;
        }

        /// <summary>
        /// Generates count randomized Vector2 distractors around correctValue, appends the correct value, and shuffles.
        /// Returns a list of length count + 1 where exactly one entry equals correctValue.
        /// </summary>
        /// <param name="correctValue">Ground-truth polyp size (width × height in mm).</param>
        /// <param name="count">Number of distractor values to generate.</param>
        /// <remarks>
        /// Each component is drawn from [max(0.1, correct − 1), correct + 1] and rounded to 0.5 mm steps.
        /// A HashSet prevents duplicate distractors; the loop retries until count unique values are collected.
        /// </remarks>
        protected List<Vector2> GenerateRandomVector2FromValue(Vector2 correctValue, int count)
        {
            System.Random random = new System.Random();
            HashSet<Vector2> randomVectors = new HashSet<Vector2>(); // Using a HashSet to prevent duplicates

            // Rounding helper for multiples of 5 with max 1 decimal place
            float RoundToMultipleOfFive(float value)
            {
                float rounded = (float)(Math.Round(value * 10.0f / 5.0f) * 5.0f / 10.0f);
                return Math.Max(0.5f, rounded);
            }

            // Function to generate a random value within +/- 1 range from the correct value
            float GenerateRandomComponent(float correctComponent)
            {
                float minValue = Math.Max(0.1f, correctComponent - 1); // Ensuring no values less than 0.1
                float maxValue = correctComponent + 1;

                float randomValue = (float)(random.NextDouble() * (maxValue - minValue) + minValue);
                return RoundToMultipleOfFive(randomValue);
            }

            while (randomVectors.Count < count)
            {
                // Generate random components for Vector2
                float randomA = GenerateRandomComponent(correctValue.x);
                float randomB = GenerateRandomComponent(correctValue.y);

                Vector2 randomVector = new Vector2(randomA, randomB);

                // Ensure uniqueness and add to the set
                randomVectors.Add(randomVector);
            }

            // Add the correctValue to the set
            randomVectors.Add(correctValue);

            // Convert HashSet to a List and shuffle it
            List<Vector2> resultList = randomVectors.OrderBy(x => random.Next()).ToList();

            return resultList;
        }

        /// <summary>
        /// Computes a weighted correctness score (0–10) for one polyp identification attempt.
        /// Throws ArgumentException if the four configured weights do not sum to 1.0.
        /// </summary>
        /// <param name="isSizeCorrect">True if the trainee selected the correct size.</param>
        /// <param name="isLocationCorrect">True if the trainee selected the correct intestinal location.</param>
        /// <param name="isParisClassCorrect">True if the trainee selected the correct Paris morphology class.</param>
        /// <param name="isJnetClassCorrect">True if the trainee selected the correct JNET neoplasia class.</param>
        /// <remarks>
        /// Formula: (sum of weights for correct fields) × 10, rounded to 2 decimal places.
        /// Example with default equal weights: 3 correct fields → 0.75 × 10 = 7.50.
        /// </remarks>
        public float CalculateAnswerScore(bool isSizeCorrect, bool isLocationCorrect, bool isParisClassCorrect, bool isJnetClassCorrect)
        {
            float totalWeight = _sizeWeight + _locationWeight + _parisClassWeight + _jnetClassWeight;
            if (Math.Abs(totalWeight - 1) > 0.001)
            {
                throw new ArgumentException("Weights must sum up to 1.");
            }

            float correctWeightedFields =
                (isSizeCorrect ? _sizeWeight : 0) +
                (isLocationCorrect ? _locationWeight : 0) +
                (isParisClassCorrect ? _parisClassWeight : 0) +
                (isJnetClassCorrect ? _jnetClassWeight : 0);

            float answerScore = correctWeightedFields * 10;
            float roundedAnswerScore = (float)Math.Round(answerScore, 2);

            return roundedAnswerScore;
        }

        /// <summary>
        /// Resets all identification state for a new training session.
        /// Clears answers, resets count, and clears the ongoing flag.
        /// </summary>
        public void Reset()
        {
            IdentifiedPolypCount = 0;
            IdentificationAnswers.Clear();
            _isPolypIdentificationOngoing = false;
        }
    }
}
