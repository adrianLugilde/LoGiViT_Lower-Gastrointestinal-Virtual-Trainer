using System;
using System.Collections.Generic;
using UnityEngine;

namespace LargeIntestine
{
    public enum DiseaseType
    {
        Polyps,
        Diverticula,
        Inflamatory,
        Angiodysplasia,
        Lypoma
    }

    [Serializable]
    public class Li_Disease
    {
        public List<LI_Segment> affectedSegments;
        public DiseaseType diseaseType;
        public float severity;
        //public bool hasDisease;

        public Li_Disease()
        {

        }
    }

    [Serializable]
    public class Polyps : Li_Disease
    {
        public Material material;
        //public List<int> avaliableSegments = new List<int> { 0, 1, 2, 3, 4 };
        public Polyps(float severity, List<LI_Segment> affectedSegments)
        {
            this.affectedSegments = affectedSegments;
            this.severity = severity;
            this.diseaseType = DiseaseType.Polyps;
        }
    }

    [Serializable]
    public class Diverticula : Li_Disease
    {

        public Material material;
        //public List<int> avaliableSegments = new List<int> { 0, 1, 2, 3, 4 };
        public Diverticula(float severity, List<LI_Segment> affectedSegments)
        {
            this.affectedSegments = affectedSegments;
            this.severity = severity;
            this.diseaseType = DiseaseType.Diverticula;
        }
    }

    [Serializable]
    public class Inflamatory : Li_Disease
    {
        public Material material;
        //public List<int> avaliableSegments = new List<int> { 0, 1, 2, 3, 4 };
        public Inflamatory(float severity, List<LI_Segment> affectedSegments)
        {
            diseaseType = DiseaseType.Diverticula;
        }
    }

    [Serializable]
    public class Angiodysplasia : Li_Disease
    {

        public Material material;
        //public List<int> avaliableSegments = new List<int> { 0, 1, 2, 3, 4 };
        public Angiodysplasia(float severity, List<LI_Segment> affectedSegments)
        {
            diseaseType = DiseaseType.Diverticula;
        }
    }

    [Serializable]
    public class Lypoma : Li_Disease
    {
        [Range(0f, 1)]
        public float probability;
        public Material material;
        //public List<int> avaliableSegments = new List<int> { 0, 1, 2, 3, 4 };
        public Lypoma(float severity, List<LI_Segment> affectedSegments)
        {
            diseaseType = DiseaseType.Diverticula;
        }
    }
}




    
