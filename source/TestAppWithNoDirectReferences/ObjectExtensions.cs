namespace TestAppWithNoDirectReferences
{
    public static class ObjectExtensions
    {
        public static void SetProperty<T>(this object obj, string propertyName, object value)
        {
            var propInfo = obj.GetType().GetProperty(propertyName);
            if (propInfo == null)
                return;
            if (!typeof (T).IsAssignableFrom(propInfo.PropertyType))
                return;
            propInfo.SetValue(obj, value);
        }
    }
}