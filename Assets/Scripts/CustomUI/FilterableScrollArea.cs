using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Messages;
using System;

namespace CustomUI
{
    /// <summary>
    /// FilterableScrollArea is an abstract class that extends BaseScrollArea to provide filtering functionality for scrollable UI areas.
    /// It allows filtering of elements based on a text input field and provides methods to manage the filtered elements.
    /// </summary>
    /// <typeparam name="TElement">The type of the UI element to be displayed in the scroll area.</typeparam>
    /// <typeparam name="TData">The type of data associated with each UI element.</typeparam>

    public abstract class FilterableScrollArea<TElement, TData> : BaseScrollArea
        where TElement : MonoBehaviour
        where TData : class
    {
        [Header("Filter elements")]
        public TElement SelectedElement {get; private set;}
        public Sprite FilterImageSprite;
        public Sprite CancelFilterImageSprite;
        public Image FilterButtonImage;
        public GameObject FilterGameObject;
        public Action<int> OnFilterEmptyResults;


        private ButtonController _filterButton;
        private TMP_InputField _filterInputField;

        [HideInInspector] public List<TElement> ElementList = new List<TElement>();
        public Action<TElement> OnScrollAreaElementClicked;

        protected virtual void Start()
        {
            if (FilterGameObject)
            {
                _filterButton = FilterGameObject.GetComponentInChildren<ButtonController>();
                _filterInputField = FilterGameObject.GetComponentInChildren<TMP_InputField>();
                _filterButton.OnSelected += () => FilterByName(_filterInputField);
            }
        }

        public virtual void Fill()
        {
            Empty();
            var dataList = LoadDataList();
            dataList = dataList.OrderBy(item => GetFileName(item)).ToList();

            for (int i = 0; i < dataList.Count; i++)
            {
                ElementList.Add(AddElement(dataList[i], i));
            }

            Scrollbar.value = 1;
        }

        public virtual void UpdateContent(TData data)
        {
            var dataList = ElementList.Select(element => GetElementData(element)).ToList();
            var itemIndex = dataList.FindIndex(item =>
                GetFileID(item) == GetFileID(data) ||
                string.Equals(GetFileName(item), GetFileName(data), StringComparison.OrdinalIgnoreCase));

            if (itemIndex != -1)
            {
                dataList[itemIndex] = data;
                SetElementData(ElementList[itemIndex], data);
                UpdateElementPreview(ElementList[itemIndex]);
            }
            else
            {
                dataList.Add(data);
                dataList = dataList.OrderBy(item => GetFileName(item)).ToList();
                ElementList.Add(AddElement(data, dataList.IndexOf(data)));
            }
        }

        public void SetSelectedElement(TElement element)
        {
            SelectedElement = element;
        }

        public void FilterByName(TMP_InputField input)
        {
            if (input.text == "")
            {
                DisplayNoResultsWarning();
                return;
            }

            var toInclude = ElementList.Where(element =>
                GetFileName(GetElementData(element)).ToLower().Contains(input.text.ToLower())).ToList();

            if (toInclude.Count == 0)
            {
                DisplayNoResultsWarning();
                return;
            }
            FilterElements(toInclude);
        }

        protected virtual void DisplayNoResultsWarning()
        {
            OnFilterEmptyResults?.Invoke(BasicMessages.SearchWithoutResults);
        }

        public virtual void FilterElements(List<TElement> toInclude)
        {
            ElementList.ForEach(element => element.gameObject.SetActive(toInclude.Contains(element)));
            FilterButtonImage.sprite = CancelFilterImageSprite;
            _filterButton.OnSelected = null;
            _filterButton.OnSelected += RemoveFilter;
        }

        public virtual void RemoveFilter()
        {
            ElementList.ForEach(element => element.gameObject.SetActive(true));
            _filterInputField.text = "";
            FilterButtonImage.sprite = FilterImageSprite;
            _filterButton.OnSelected = null;
            _filterButton.OnSelected += () => FilterByName(_filterInputField);
        }

        public virtual void Empty()
        {
            base.Reset();
            ClearElements();
            ElementList = new List<TElement>();
            OnEmptyComplete();
        }

        protected virtual void ClearElements()
        {
            foreach (var element in ElementList)
            {
                Destroy(element.gameObject);
            }
        }

        protected abstract List<TData> LoadDataList();
        protected abstract TElement AddElement(TData data, int index);
        protected abstract TData GetElementData(TElement element);
        protected abstract void SetElementData(TElement element, TData data);
        protected abstract void UpdateElementPreview(TElement element);
        protected abstract string GetFileName(TData data);
        protected abstract long GetFileID(TData data);
        protected abstract void OnEmptyComplete();
    }
}