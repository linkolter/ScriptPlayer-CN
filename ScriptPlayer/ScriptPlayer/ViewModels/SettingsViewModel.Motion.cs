using System;
using System.Xml.Serialization;

namespace ScriptPlayer.ViewModels
{
    public partial class SettingsViewModel
    {
        private string _interfaceMotionMode = "Light";
        private bool _motionModeExplicit;
        private bool _simplifyInterfaceAnimations = true;
        private bool _animateInteraction = true, _animateSwitches = true, _animatePages;
        private bool _animateTransport = true, _animateMenus = true, _animatePanels, _animateCovers;
        private bool _autoPlayCoverAnimations;

        public static bool IsMotionPreference(string name) => name == nameof(EnableInterfaceAnimations)
            || name == nameof(InterfaceMotionMode) || name == nameof(SimplifyInterfaceAnimations)
            || name == nameof(AnimateInteraction) || name == nameof(AnimateSwitches)
            || name == nameof(AnimatePages) || name == nameof(AnimateTransport)
            || name == nameof(AnimateMenus) || name == nameof(AnimatePanels)
            || name == nameof(AnimateCovers) || name == nameof(AutoPlayCoverAnimations);

        public string InterfaceMotionMode
        {
            get => _interfaceMotionMode;
            set
            {
                string mode = value == "Off" || value == "Standard" || value == "Custom" ? value : "Light";
                _motionModeExplicit = true;
                if (mode == _interfaceMotionMode) return;
                _interfaceMotionMode = mode;
                _enableInterfaceAnimations = mode != "Off";
                if (mode == "Light" || mode == "Standard")
                {
                    _animateInteraction = _animateSwitches = _animateTransport = _animateMenus = true;
                    _animatePages = _animatePanels = mode == "Standard";
                    _animateCovers = false;
                }
                NotifyMotionPreferences();
            }
        }

        // Keep the old XML element, but let an explicit new mode win in either element order.
        [XmlElement("EnableInterfaceAnimations")]
        public bool LegacyInterfaceAnimations
        {
            get => _enableInterfaceAnimations;
            set
            {
                if (_motionModeExplicit) return;
                _interfaceMotionMode = value ? "Custom" : "Off";
                _animatePages = _animateMenus = true;
                _animateInteraction = _animateSwitches = _animateTransport = _animatePanels = _animateCovers = false;
                _autoPlayCoverAnimations = true;
                _enableInterfaceAnimations = value;
            }
        }

        private void SetLegacyAnimationPreference(bool enabled)
        {
            if (enabled == _enableInterfaceAnimations) return;
            InterfaceMotionMode = enabled ? "Custom" : "Off";
        }

        public bool SimplifyInterfaceAnimations { get => _simplifyInterfaceAnimations; set { if (_simplifyInterfaceAnimations == value) return; _simplifyInterfaceAnimations = value; OnPropertyChanged(); } }
        public bool AutoPlayCoverAnimations { get => _autoPlayCoverAnimations; set { if (_autoPlayCoverAnimations == value) return; _autoPlayCoverAnimations = value; OnPropertyChanged(); } }
        [XmlIgnore]
        public bool AnimateInteraction { get => _animateInteraction; set => SetMotionFlag(ref _animateInteraction, value, nameof(AnimateInteraction)); }
        [XmlIgnore]
        public bool AnimateSwitches { get => _animateSwitches; set => SetMotionFlag(ref _animateSwitches, value, nameof(AnimateSwitches)); }
        [XmlIgnore]
        public bool AnimatePages { get => _animatePages; set => SetMotionFlag(ref _animatePages, value, nameof(AnimatePages)); }
        [XmlIgnore]
        public bool AnimateTransport { get => _animateTransport; set => SetMotionFlag(ref _animateTransport, value, nameof(AnimateTransport)); }
        [XmlIgnore]
        public bool AnimateMenus { get => _animateMenus; set => SetMotionFlag(ref _animateMenus, value, nameof(AnimateMenus)); }
        [XmlIgnore]
        public bool AnimatePanels { get => _animatePanels; set => SetMotionFlag(ref _animatePanels, value, nameof(AnimatePanels)); }
        [XmlIgnore]
        public bool AnimateCovers { get => _animateCovers; set => SetMotionFlag(ref _animateCovers, value, nameof(AnimateCovers)); }

        // Loading individual saved flags must not turn a saved Off/Standard preset into Custom.
        [XmlElement("AnimateInteraction")] public bool SavedAnimateInteraction { get => _animateInteraction; set => _animateInteraction = value; }
        [XmlElement("AnimateSwitches")] public bool SavedAnimateSwitches { get => _animateSwitches; set => _animateSwitches = value; }
        [XmlElement("AnimatePages")] public bool SavedAnimatePages { get => _animatePages; set => _animatePages = value; }
        [XmlElement("AnimateTransport")] public bool SavedAnimateTransport { get => _animateTransport; set => _animateTransport = value; }
        [XmlElement("AnimateMenus")] public bool SavedAnimateMenus { get => _animateMenus; set => _animateMenus = value; }
        [XmlElement("AnimatePanels")] public bool SavedAnimatePanels { get => _animatePanels; set => _animatePanels = value; }
        [XmlElement("AnimateCovers")] public bool SavedAnimateCovers { get => _animateCovers; set => _animateCovers = value; }

        private void SetMotionFlag(ref bool field, bool value, string name)
        {
            if (field == value) return;
            field = value;
            _motionModeExplicit = true;
            _interfaceMotionMode = "Custom";
            _enableInterfaceAnimations = true;
            OnPropertyChanged(name);
            OnPropertyChanged(nameof(InterfaceMotionMode));
            OnPropertyChanged(nameof(EnableInterfaceAnimations));
        }

        private void NotifyMotionPreferences()
        {
            foreach (string name in new[] { nameof(InterfaceMotionMode), nameof(EnableInterfaceAnimations),
                nameof(AnimateInteraction), nameof(AnimateSwitches), nameof(AnimatePages), nameof(AnimateTransport),
                nameof(AnimateMenus), nameof(AnimatePanels), nameof(AnimateCovers), nameof(AutoPlayCoverAnimations) }) OnPropertyChanged(name);
        }

        private void CopyMotionPreferencesTo(SettingsViewModel copy)
        {
            copy._interfaceMotionMode = _interfaceMotionMode;
            copy._motionModeExplicit = _motionModeExplicit;
            copy._enableInterfaceAnimations = _enableInterfaceAnimations;
            copy._simplifyInterfaceAnimations = _simplifyInterfaceAnimations;
            copy._animateInteraction = _animateInteraction; copy._animateSwitches = _animateSwitches;
            copy._animatePages = _animatePages; copy._animateTransport = _animateTransport;
            copy._animateMenus = _animateMenus; copy._animatePanels = _animatePanels; copy._animateCovers = _animateCovers;
            copy._autoPlayCoverAnimations = _autoPlayCoverAnimations;
        }
    }
}
