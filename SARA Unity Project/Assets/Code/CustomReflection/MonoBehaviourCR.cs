
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using UnityEngine;

namespace teadrinker
{
    public class MonoBehaviourCR : MonoBehaviour, CustomReflectionSupport
	{
        private CustomReflection _customRefl;
        public CustomReflection GetCustomReflection()
        {
            if (_customRefl == null)
                _customRefl = GenerateCustomReflection();
            return _customRefl;
        }
        public virtual CustomReflection GenerateCustomReflection()
        {
            return new CustomReflection();
        }

	}
}