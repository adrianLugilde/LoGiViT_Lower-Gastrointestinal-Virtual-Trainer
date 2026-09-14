namespace PolypTraining
{

    public readonly struct PolypIdentificationSnapshot
    {
        public readonly string SizeAnswer;
        public readonly int DiseaseLocationAnswer;
        public readonly string ParisClassAnswer;
        public readonly string JnetClassAnswer;

        public PolypIdentificationSnapshot(
            string sizeAnswer,
            int diseaseLocationAnswer,
            string parisClassAnswer,
            string jnetClassAnswer)
        {
            SizeAnswer = sizeAnswer;
            DiseaseLocationAnswer = diseaseLocationAnswer;
            ParisClassAnswer = parisClassAnswer;
            JnetClassAnswer = jnetClassAnswer;
        }
    }
}