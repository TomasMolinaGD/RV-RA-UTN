using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace CarShowcase.Editor
{
    public sealed class ARCoreManifestCompatibility : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
                return;

            var document = new XmlDocument();
            document.Load(manifestPath);

            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("android", AndroidNamespace);

            XmlNode depthFeature = document.SelectSingleNode(
                "/manifest/uses-feature[@android:name='com.google.ar.core.depth']",
                namespaces);

            if (depthFeature?.Attributes == null)
                return;

            XmlAttribute required = depthFeature.Attributes["required", AndroidNamespace]
                ?? document.CreateAttribute("android", "required", AndroidNamespace);
            required.Value = "false";

            if (required.OwnerElement == null)
                depthFeature.Attributes.Append(required);

            document.Save(manifestPath);
        }
    }
}
