namespace PolypTraining
{
    public readonly struct PolypIdentificationAnswer
    {
        public readonly Polyp Polyp;
        public readonly string Size;
        public readonly bool IsSizeCorrect;
        public readonly Disease.IntestineLocation? Location;
        public readonly bool IsLocationCorrect;
        public readonly Polyp.ParisClassification? ParisClass;
        public readonly bool IsParisClassCorrect;
        public readonly Polyp.JNETClassification? JnetClass;
        public readonly bool IsJnetClassCorrect;
        public readonly bool IsIdentificationCorrect;
        public readonly bool WasNotIdentified;
        public readonly float Score;

        public PolypIdentificationAnswer(
            Polyp polyp,
            string size,
            bool isSizeCorrect,
            Disease.IntestineLocation? location,
            bool isLocationCorrect,
            Polyp.ParisClassification? parisClass,
            bool isParisClassCorrect,
            Polyp.JNETClassification? jnetClass,
            bool isJnetClassCorrect,
            float score,
            bool wasNotIdentified = false)
        {
            this.Polyp = polyp;
            this.Size = size;
            this.IsSizeCorrect = isSizeCorrect;
            this.Location = location;
            this.IsLocationCorrect = isLocationCorrect;
            this.ParisClass = parisClass;
            this.IsParisClassCorrect = isParisClassCorrect;
            this.JnetClass = jnetClass;
            this.IsJnetClassCorrect = isJnetClassCorrect;
            this.Score = score;
            IsIdentificationCorrect = IsSizeCorrect && IsLocationCorrect && IsParisClassCorrect && IsJnetClassCorrect;
            WasNotIdentified = wasNotIdentified;
        }
    }
}
