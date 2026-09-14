// ============================================================================
// LargeIntestineSectionInteractablesController.cs
// 
// Manages XR-based section interactables for the large intestine model.
// Part of the Model Editor system - implements IModelSectionInteractablesController
// to provide a consistent interface for section interaction management.
// 
// Usage:
//   1. Call Initialize() with the model generator and selection controller
//   2. Call AddSectionsInteractables() after the model is generated
//   3. Use Enable/Disable methods to control interaction availability
//   4. Use material methods to switch between editing and viewing modes
// ============================================================================

using CustomUI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModelEditor
{
    /// <summary>
    /// Controller for managing large intestine section interactables.
    /// Creates <see cref="XRIntestineSectionInteractable"/> components for XR-based interaction.
    /// </summary>
    /// <remarks>
    /// This is the concrete implementation for large intestine organs.
    /// For other organ types, create a new controller implementing <see cref="IModelSectionInteractablesController"/>.
    /// </remarks>
    [DefaultExecutionOrder(5)]
    public class LargeIntestineSectionsInteractablesController : MonoBehaviour, IModelSectionInteractablesController
    {
        #region Serialized Fields
        
        [Tooltip("Material applied to sections when in transparent/editing mode")]
        [SerializeField] private Material _transparentMaterial;
        
        #endregion
        
        #region Private Fields
        
        private ISplineModelGenerator _modelGenerator;
        private SelectionController _selectionController;
        private List<XRIntestineSectionInteractable> _sectionInteractables;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Typed accessor for internal use within the large intestine system.
        /// Provides access to the concrete <see cref="XRIntestineSectionInteractable"/> type.
        /// </summary>
        public IReadOnlyList<XRIntestineSectionInteractable> SectionInteractables => _sectionInteractables;
        
        /// <summary>
        /// Explicit interface implementation returning the base interface type.
        /// Used by systems that work with any <see cref="IModelSectionInteractablesController"/>.
        /// </summary>
        IReadOnlyList<IModelSectionInteractable> IModelSectionInteractablesController.SectionInteractables => 
            _sectionInteractables.Cast<IModelSectionInteractable>().ToList();
        
        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            _sectionInteractables = new List<XRIntestineSectionInteractable>();
        }
        
        #endregion
        
        #region IModelSectionInteractablesController Implementation

        /// <summary>
        /// Initializes the controller with required dependencies.
        /// Must be called before <see cref="AddSectionsInteractables"/>.
        /// </summary>
        /// <param name="modelGenerator">The model generator that provides section data</param>
        /// <param name="selectionController">The selection controller for handling user selection</param>
        public void Initialize(ISplineModelGenerator modelGenerator, SelectionController selectionController)
        {
            _modelGenerator = modelGenerator;
            _selectionController = selectionController;
        }

        /// <summary>
        /// Creates XR interactables for all sections in the model.
        /// Each section gets an <see cref="XRIntestineSectionInteractable"/> component attached.
        /// </summary>
        public virtual void AddSectionsInteractables()
        {
            var sections = _modelGenerator.Sections;
            var sectionsCount = sections.Count;

            // Create an interactable for each section in the model
            for (int i = 0; i < sectionsCount; i++)
            {
                var interactable = CreateInteractable(sections[i], i);
                _sectionInteractables.Add(interactable);
            }
        }

        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Creates a single XR interactable for a section.
        /// </summary>
        /// <param name="section">The section to make interactable</param>
        /// <param name="sectionIndex">The index of the section in the model</param>
        /// <returns>The created interactable component</returns>
        private XRIntestineSectionInteractable CreateInteractable(IModelSection section, int sectionIndex)
        {
            // Add XR interactable component to the section's renderer GameObject
            var interactable = section.Renderer.gameObject.AddComponent<XRIntestineSectionInteractable>();
            interactable.Initialize(_selectionController, sectionIndex);
            return interactable;
        }
        
        #endregion

        #region Interaction Control
        
        /// <summary>
        /// Disables all section interactables, preventing user interaction.
        /// Typically called when switching to a different editing mode.
        /// </summary>
        public void DisableSectionInteractables()
        {
            Debug.Log("Disabling section interactables");
            foreach (var interactable in _sectionInteractables)
            {
                interactable.DisableInteractable();
            }
        }

        /// <summary>
        /// Enables all section interactables, allowing user interaction.
        /// Typically called when entering section editing mode.
        /// </summary>
        public void EnableSectionInteractables()
        {
            foreach (var interactable in _sectionInteractables)
            {
                interactable.EnableInteractable();
            }
        }
        
        #endregion
        
        #region Material Management

        /// <summary>
        /// Restores original materials to all sections and switches to viewing mode.
        /// Hides the segmented model and enables the welded model collider.
        /// </summary>
        public void RestoreSectionsOriginalMaterials()
        {
            // Restore each section's original materials
            foreach (var interactable in _sectionInteractables)
            {
                interactable.RestoreOriginalMaterials();
            }
            
            // Re-enable the welded model collider for full-model interaction
            if (_modelGenerator.WeldedModelCollider != null)
                _modelGenerator.WeldedModelCollider.enabled = true;
            
            // Hide the segmented model (individual sections)
            if (_modelGenerator.SegmentedModelGO != null)
                _modelGenerator.SegmentedModelGO.SetActive(false);
        }

        /// <summary>
        /// Applies transparent materials to all sections and switches to editing mode.
        /// Shows the segmented model and disables the welded model collider.
        /// </summary>
        public void SetTransparentSectionsMaterials()
        {
            // Disable the welded model collider to allow individual section selection
            if (_modelGenerator.WeldedModelCollider != null)
                _modelGenerator.WeldedModelCollider.enabled = false;
            
            // Show the segmented model (individual sections)
            if (_modelGenerator.SegmentedModelGO != null)
                _modelGenerator.SegmentedModelGO.SetActive(true);
            
            // Apply transparent material to each section
            foreach (var interactable in _sectionInteractables)
            {
                interactable.SetMaterial(_transparentMaterial);
            }
        }
        
        #endregion
    }
}