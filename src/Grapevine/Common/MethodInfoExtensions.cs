﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Grapevine.Exceptions;

namespace Grapevine.Common
{
    public static class MethodInfoExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        internal static Action<T> ConvertToAction<T>(this MethodInfo methodInfo)
        {
            methodInfo.IsRestRouteEligible<T>(true); // will throw an aggregate exception if the method is not eligible

            // Static method
            if (methodInfo.IsStatic || methodInfo.ReflectedType == null)
            {
                return context => { methodInfo.Invoke(null, new object[] { context }); };
            }

            // Generate a new instance every invocation
            return context =>
            {
                var instance = Activator.CreateInstance(methodInfo.ReflectedType);
                try
                {
                    methodInfo.Invoke(instance, new object[] { context });
                }
                finally
                {
                    instance.TryDisposing();
                }
            };
        }

        /// <summary>
        /// Returns a value indicating that the referenced method can be used to create a Route object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="method"></param>
        /// <param name="throwExceptionWhenFalse"></param>
        /// <returns></returns>
        internal static bool IsRestRouteEligible<T>(this MethodInfo method, bool throwExceptionWhenFalse = false)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var exceptions = new List<Exception>();

            // Can the method be invoked?
            if (!method.CanInvoke()) exceptions.Add(new InvalidRouteMethodException($"{method.Name} cannot be invoked"));

            // Does the type have a parameterless constructor?
            if (method.ReflectedType != null && !method.ReflectedType.HasParameterlessConstructor()) exceptions.Add(new InvalidRouteMethodException($"{method.ReflectedType} does not have a parameterless constructor"));

            // Can not have a special name (getters and setters)
            if (method.IsSpecialName) exceptions.Add(new InvalidRouteMethodException($"{method.Name} may be treated in a special way by some compilers (such as property accessors and operator overloading methods)"));

            var args = method.GetParameters();

            // Method must have only one argument
            if (args.Length != 1) exceptions.Add(new InvalidRouteMethodException($"{method.Name} must accept one and only one argument"));

            // First argument to method must be of type T
            if (args.Length > 0 && args[0].ParameterType != typeof(T)) exceptions.Add(new InvalidRouteMethodException($"{method.Name}: first argument must be of type {typeof(T).Name}"));

            // Return boolean value
            if (exceptions.Count == 0) return true;
            if (!throwExceptionWhenFalse) return false;

            // Throw exeception
            throw new InvalidRouteMethodExceptions(exceptions.ToArray());
        }

        /// <summary>
        /// Returns a value indicating that the method can be invoked via reflection
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        internal static bool CanInvoke(this MethodInfo methodInfo)
        {
            /*
            * The first set of checks are on the method itself:
            * - Static methods can always be invoked
            * - Abstract methods can never be invoked
            */
            if (methodInfo.IsStatic) return true;
            if (methodInfo.IsAbstract) return false;

            /*
             * The second set of checks are on the type the method
             * comes from. This uses the ReflectedType property,
             * which will be the same property used by the Route
             * class to invoke the method later on.
             * - ReflectedType can not be null
             * - ReflectedType can not be abstract
             * - ReflectedType must be a class (vs an interface or struct, etc.)
             */
            var type = methodInfo.ReflectedType;
            if (type == null) return false;
            if (!type.IsClass) return false;
            if (type.IsAbstract) return false;

            /*
             * If these checks have all passed, then we can be fairly certain
             * that the method can be invoked later on during routing.
             */
            return true;
        }
    }
}