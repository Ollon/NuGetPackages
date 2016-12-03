using System;
using Microsoft.VisualStudio.Shell;
using System.Globalization;
using System.ComponentModel;
using System.IO;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.PlatformUI;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Collections.Generic;

namespace Ollon.VisualStudio.Shell.Attributes
{
    /// <summary>
    /// Registers a class in Visual Studio as a VSPackage
    /// that contains a Menu Resource "Menus.ctmenu" with a 
    /// version of 1
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class VisualStudioPackageAttribute : RegistrationAttribute
    {
        public VisualStudioPackageAttribute(string productName, string productDetails, string productId)
        {
            Contract.Assert(!string.IsNullOrEmpty(productId));
            Contract.Assert(!string.IsNullOrEmpty(productName));
            Contract.Assert(!string.IsNullOrEmpty(productDetails));

            ProductId = productId;
            ProductName = productName;
            ProductDetails = productDetails;
            UseManagedResourcesOnly = true;
        }

        public RegistrationMethod RegisterUsing { get; set; }
        public bool UseManagedResourcesOnly { get; set; }
        public bool AllowsBackgroundLoading { get; set; }
        public string LanguageIndependentName { get; set; }
        public string IconMappingFilename { get; set; }

        public string ResourceID
        {
            get
            {
                return "Menus.ctmenu";
            }
        }

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public string ProductId { get; }
        public string ProductName { get; }
        public string ProductDetails { get; }

        public override void Register(RegistrationContext context)
        {
            RegisterPackage(context);
            RegisterInstalledProduct(context);
            RegisterMenuResource(context);
        }

        public override void Unregister(RegistrationContext context)
        {
            UnregisterPackage(context);
            UnregisterInstalledProduct(context);
            UnregisterMenuResource(context);
        }


        private static string GetRegistryValueName(RegistrationContext context)
        {
            return context.ComponentType.GUID.ToString("B");
        }

        private static string GetFullyQualifiedIconMappingFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return string.Empty;
            if (Path.IsPathRooted(filename))
                return filename;
            switch (filename[0])
            {
                case '$':
                case '%':
                    return filename;
                default:
                    return Path.Combine("$PackageFolder$", filename);
            }
        }

        private static bool IsValidPath(string path)
        {
            foreach (char invalidPathChar in Path.GetInvalidPathChars())
            {
                if (path.ToCharArray().Contains(invalidPathChar))
                    return false;
            }
            return true;
        }

        private void RegisterMenuResource(RegistrationContext context)
        {
            string iconMappingFilename = GetFullyQualifiedIconMappingFilename(IconMappingFilename);
            string format = iconMappingFilename == string.Empty ? Resources.Reg_NotifyMenuResource : Resources.Reg_NotifyMenuResourceWithMapping;
            context.Log.WriteLine(string.Format(Resources.Culture, format, ResourceID, Version, iconMappingFilename));
            using (Key key = context.CreateKey("Menus"))
            {
                string str = string.Format(CultureInfo.InvariantCulture, ", {0}, {1}", ResourceID, Version);
                if (iconMappingFilename != string.Empty)
                    str = str + ", " + iconMappingFilename;
                key.SetValue(GetRegistryValueName(context), str);
            }
        }

        private void UnregisterMenuResource(RegistrationContext context)
        {
            context.RemoveValue("Menus", GetRegistryValueName(context));
        }

        private string GetNonEmptyName(RegistrationContext context)
        {
            string str = LanguageIndependentName;
            if (str != null)
                str = str.Trim();
            if (string.IsNullOrEmpty(str))
                str = context.ComponentType.Name;
            return str;
        }
        private string InstalledProductRegKeyName(RegistrationContext context)
        {
            return string.Format(CultureInfo.InvariantCulture, "InstalledProducts\\{0}", GetNonEmptyName(context));
        }
        private void RegisterInstalledProduct(RegistrationContext context)
        {
            context.Log.WriteLine(Resources.Reg_NotifyInstalledProduct, GetNonEmptyName(context), ProductId ?? ProductName);
            using (Key key = context.CreateKey(InstalledProductRegKeyName(context)))
            {

                key.SetValue("Package", context.ComponentType.GUID.ToString("B"));
                if (!string.IsNullOrEmpty(LanguageIndependentName))
                    key.SetValue("UseRegNameAsSplashName", 1);

                key.SetValue("PID", ProductId);
                if (!string.IsNullOrEmpty(ProductDetails))
                    key.SetValue("ProductDetails", ProductDetails);
            }
        }
        private void UnregisterInstalledProduct(RegistrationContext context)
        {
            context.RemoveKey(InstalledProductRegKeyName(context));
        }
        private string PackageRegKeyName(RegistrationContext context)
        {
            return string.Format(CultureInfo.InvariantCulture, "Packages\\{0}", context.ComponentType.GUID.ToString("B"));
        }
        private void RegisterPackage(RegistrationContext context)
        {
            Type componentType = context.ComponentType;
            context.Log.WriteLine(string.Format(Resources.Culture, Resources.Reg_NotifyPackage, componentType.Name, componentType.GUID.ToString("B")));
            Key key1 = null;
            try
            {
                key1 = context.CreateKey(PackageRegKeyName(context));
                DescriptionAttribute attribute = TypeDescriptor.GetAttributes(componentType)[typeof(DescriptionAttribute)] as DescriptionAttribute;
                if (attribute != null && !string.IsNullOrEmpty(attribute.Description))
                    key1.SetValue(string.Empty, attribute.Description);
                else
                    key1.SetValue(string.Empty, componentType.Name);
                key1.SetValue("InprocServer32", context.InprocServerPath);
                key1.SetValue("Class", componentType.FullName);
                RegisterUsing = context.RegistrationMethod;
                switch (RegisterUsing)
                {
                    case RegistrationMethod.Default:
                    case RegistrationMethod.CodeBase:
                        key1.SetValue("Assembly", componentType.Assembly.FullName);
                        break;
                    case RegistrationMethod.Assembly:
                        key1.SetValue("CodeBase", context.CodeBase);
                        break;
                }

                //if (!UseManagedResourcesOnly)
                //{
                //    try
                //    {
                //        key2 = key1.CreateSubkey("SatelliteDll");
                //        string str = SatellitePath == null ? context.ComponentPath : context.EscapePath(SatellitePath);
                //        key2.SetValue("Path", str);
                //        key2.SetValue("DllName", string.Format(CultureInfo.InvariantCulture, "{0}UI.dll", Path.GetFileNameWithoutExtension(componentType.Assembly.ManifestModule.Name)));
                //    }
                //    finally
                //    {
                //        if (key2 != null)
                //            key2.Close();
                //    }
                //}
                if (AllowsBackgroundLoading)
                    key1.SetValue("AllowsBackgroundLoad", true);
                if (!typeof(IVsPackageDynamicToolOwner).IsAssignableFrom(context.ComponentType) && !typeof(IVsPackageDynamicToolOwnerEx).IsAssignableFrom(context.ComponentType))
                    return;
                key1.SetValue("SupportsDynamicToolOwner", Boxes.BooleanTrue);
            }
            finally
            {
                if (key1 != null)
                    key1.Close();
            }
        }
        private void UnregisterPackage(RegistrationContext context)
        {
            context.RemoveKey(PackageRegKeyName(context));
        }


        private void RegisterService(RegistrationContext context)
        {

        }

        private void UnregisterService(RegistrationContext context)
        {

        }
    }
}
