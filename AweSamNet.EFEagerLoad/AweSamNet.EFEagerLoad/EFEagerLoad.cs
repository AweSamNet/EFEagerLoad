using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reflection;

namespace AweSamNet.Data
{
    /// <summary>
    /// Enhances existing IQueriables with all required .Inclue(...) invocations.  
    /// </summary>
    /// <remarks>
    /// Profiles can be used to map an entity that may or may not already be mapped to a different class property altogether.  
    /// An example of this might be:
    /// 
    ///     1. YourModels.Invoice --> Db.Invoice
    ///     2. YourModels.Invoice --> Db.InvoiceArchived
    ///     
    /// In this case, the default profile would handle the mapping of case 1 while a seperate profile (Ex: "Archives") might handle the mapping of case 2.
    /// </remarks>
    public static class EFEagerLoad
    {
        const string PROPERTY_ALREADY_MAPPED_MESSAGE = "Property: {0}.{1} is already mapped to Property: {2}.{3}";
        const string DEFAULT_PROFILE_NAME = "Default";

        private static int _MaxRecursionLevel = 3;
        /// <summary>
        /// Gets/Sets the maximum number of recursion levels to eager load.
        /// </summary>
        public static int MaxRecursionLevel
        {
            get
            {
                return _MaxRecursionLevel;
            }
            set
            {
                _MaxRecursionLevel = value;
            }
        }

        /// <summary>
        /// Represents a mapping from one Type's property to another Type's property.
        /// </summary>
        private class Mapping
        {
            public Mapping(PropertyInfo entity1, PropertyInfo entity2)
            {
                this.Entity1 = entity1;
                this.Entity2 = entity2;
            }

            public PropertyInfo Entity1 { get; private set; }
            public PropertyInfo Entity2 { get; private set; }

            public bool Contains(PropertyInfo property)
            {
                return (this.Entity1.DeclaringType.Equals(property.DeclaringType) && this.Entity1.Name.Equals(property.Name))
                    || (this.Entity2.DeclaringType.Equals(property.DeclaringType) && this.Entity2.Name.Equals(property.Name));
            }
        }

        /// <summary>
        /// Profile property mappings.
        /// </summary>
        private static Dictionary<string, List<Mapping>> Mappings = new Dictionary<string,List<Mapping>>();

        /// <summary>
        /// Adds a new mapping between one class property to another class property.
        /// </summary>
        /// <typeparam name="TEntity1">Type of Entity 1.</typeparam>
        /// <typeparam name="TEntity2">Type of Entity 2.</typeparam>
        /// <param name="entity1Selector">Selector for Entity 1.</param>
        /// <param name="entity2Selector">Selector for Entity 2.</param>
        /// <param name="profile">The profile to add the mapping to (Empty: Default profile).</param>
        /// <exception cref="ArgumentException">ArgumentException: Thrown when a passed selector has already been mapped for the given profile.</exception>
        public static void AddEFMapping<TEntity1, TEntity2>(Expression<Func<TEntity1, object>> entity1Selector, Expression<Func<TEntity2, object>> entity2Selector, string profile = DEFAULT_PROFILE_NAME)
        {
            //see if the mapping profile exists
            if (!Mappings.Keys.Contains(profile))
            {
                Mappings[profile] = new List<Mapping>();
            }

            var profileMappings = Mappings[profile];

            var entity1 = GetSelectorPropertyInfo(entity1Selector);
            var entity2 = GetSelectorPropertyInfo(entity2Selector);

            bool mappingAlreadyExists = false;
            //verify that it does not already exist
            foreach (var item in profileMappings)
            {
                if (item.Contains(entity1) || item.Contains(entity2))
                {
                    throw new ArgumentException(String.Format(PROPERTY_ALREADY_MAPPED_MESSAGE
                        , item.Entity1.DeclaringType.Name, item.Entity1.Name
                        , item.Entity2.DeclaringType.Name, item.Entity2.Name));
                }
            }

            if (!mappingAlreadyExists) //if it already exists then ignore this mapping.
            {
                profileMappings.Add(new Mapping(entity1, entity2));
            }
        }

        /// <summary>
        /// Removes all mappings for the given selector in the profile specified.
        /// </summary>
        /// <typeparam name="TEntity">Type of Entity in mapping.</typeparam>
        /// <param name="entitySelector">Selector of entity in mapping.</param>
        /// <param name="profile">The profile to add the mapping to (Empty: Default profile).</param>
        public static void RemoveMappingsFor<TEntity>(Expression<Func<TEntity, object>> entitySelector, string profile = DEFAULT_PROFILE_NAME)
        {
            var entity = GetSelectorPropertyInfo(entitySelector);

            List<Mapping> mappingsToRemove = new List<Mapping>();
            if (!Mappings.Keys.Contains(profile))
            {
                return;
            } 
            
            var profileMappings = Mappings[profile];

            //remove all mappings for this property.
            foreach (var item in profileMappings)
            {
                if (item.Contains(entity))
                {
                    mappingsToRemove.Add(item);
                }
            }

            foreach (var item in mappingsToRemove)
            {
                profileMappings.Remove(item);
            }
        }

        /// <summary>
        /// Accepts a selector and returns a PropertyInfo representing the selector.
        /// </summary>
        /// <typeparam name="TEntity">Class type of the selector</typeparam>
        /// <param name="entitySelector">Selector to process.</param>
        /// <returns>Returns a PropertyInfo representing the passed selector.</returns>
        private static PropertyInfo GetSelectorPropertyInfo<TEntity>(Expression<Func<TEntity, object>> entitySelector)
        {
            Expression body = entitySelector;
            if (body is LambdaExpression)
            {
                body = ((LambdaExpression)body).Body;
            }
            return (PropertyInfo)((MemberExpression)body).Member;
        }

        /// <summary>
        /// Enhances the passed query to include all selectors applicable based on the default mapping profile.  Returns an IEnumerable of TPrincipal using the passed mapper Func.
        /// </summary>
        /// <typeparam name="TQuery">Type of IQueryable to expect.</typeparam>
        /// <typeparam name="TPrincipal">Type of the expected returned IEnumerable.</typeparam>
        /// <param name="query">Query to enhance and execute.</param>
        /// <param name="mapperFunc">Mapper function which handles all data projection.</param>
        /// <param name="eagerLoad">Selectors indicating which entities to eagerly load.</param>
        /// <returns>Returns an IEnumerable of TPrincipal using the passed mapper Func.</returns>
        public static IEnumerable<TPrincipal> SelectWithEager<TQuery, TPrincipal>(this IQueryable<TQuery> query
            , Func<IEnumerable<TQuery>, IEnumerable<TPrincipal>> mapperFunc, params Expression<Func<object, object>>[] eagerLoad)
        {
            return SelectWithEager<TQuery, TPrincipal>(query, DEFAULT_PROFILE_NAME, mapperFunc, eagerLoad);
        }

        /// <summary>
        /// Enhances the passed query to include all selectors applicable based on the passed mapping profile.  Returns an IEnumerable of TPrincipal using the passed mapper Func.
        /// </summary>
        /// <typeparam name="TQuery">Type of IQueryable to expect.</typeparam>
        /// <typeparam name="TPrincipal">Type of the expected returned IEnumerable.</typeparam>
        /// <param name="query">Query to enhance and execute.</param>
        /// <param name="mappingProfile">Name of the mapping profile to use for this Select.</param>
        /// <param name="mapperFunc">Mapper function which handles all data projection.</param>
        /// <param name="eagerLoad">Selectors indicating which entities to eagerly load.</param>
        /// <returns>Returns an IEnumerable of TPrincipal using the passed mapper Func.</returns>
        public static IEnumerable<TPrincipal> SelectWithEager<TQuery, TPrincipal>(this IQueryable<TQuery> query
            , String mappingProfile, Func<IEnumerable<TQuery>, IEnumerable<TPrincipal>> mapperFunc, params Expression<Func<object, object>>[] eagerLoad)
        {
            Type parentType = typeof(TPrincipal);

            query = IncludeNestedEntityProperties<TQuery>(query, eagerLoad, typeof(TPrincipal), mappingProfile);

            //execute the query to pull data.
            var dbResults = query.AsEnumerable();

            return mapperFunc(dbResults);
        }

        /// <summary>
        /// Generates the .Include(...) invocations based on the passed selectors for the passed profile.  Returns IQueryable of type TQuery with any applicable includes.
        /// </summary>
        /// <typeparam name="TQuery">Type of IQueryable to expect.</typeparam>
        /// <param name="query">Query to enhance and execute.</param>
        /// <param name="eagerLoad">Selectors indicating which entities to eagerly load.</param>
        /// <param name="parentType">Type of the declaring parent of this recursion level.</param>
        /// <param name="profileName">Name of the mapping profile to use to determine mapping counterparts.</param>
        /// <returns>Returns IQueryable of type TQuery with any applicable includes.</returns>
        private static IQueryable<TQuery> IncludeNestedEntityProperties<TQuery>(IQueryable<TQuery> query, Expression<Func<object, object>>[] eagerLoad, Type parentType, string profileName)
        {
            int currentRecursionLevel = 0;
            string includeChain = String.Empty;

            return IncludeNestedEntityProperties<TQuery>(query, eagerLoad, parentType, profileName, currentRecursionLevel, includeChain);
        }

        /// <summary>
        /// Generates the .Include(...) invocations based on the passed selectors for the passed profile.  Returns IQueryable of type TQuery with any applicable includes.
        /// </summary>
        /// <typeparam name="TQuery">Type of IQueryable to expect.</typeparam>
        /// <param name="query">Query to enhance and execute.</param>
        /// <param name="eagerLoad">Selectors indicating which entities to eagerly load.</param>
        /// <param name="parentType">Type of the declaring parent of this recursion level.</param>
        /// <param name="profileName">Name of the mapping profile to use to determine mapping counterparts.</param>
        /// <param name="currentRecursionLevel">Indicates the current level of recursion.  Used to ensure recursion does not extend passed MaxRecursionLevel.</param>
        /// <param name="includeChain">Existing Chain of includes to append new include to.</param>
        /// <returns>Returns IQueryable of type TQuery with any applicable includes.</returns>
        private static IQueryable<TQuery> IncludeNestedEntityProperties<TQuery>(IQueryable<TQuery> query, Expression<Func<object, object>>[] eagerLoad, Type parentType, string profileName
            , int currentRecursionLevel, string includeChain)
        {
            if (currentRecursionLevel++ < MaxRecursionLevel && eagerLoad != null)
            {

                foreach (var item in eagerLoad)
                {
                    PropertyInfo selectorProp = GetSelectorPropertyInfo(item);
                    PropertyInfo mapToProp = GetMappedCounterPart(selectorProp, profileName);

                    if (mapToProp != null)
                    {
                        string newIncludeChain = !string.IsNullOrWhiteSpace(includeChain) ? includeChain + "." : string.Empty;
                        newIncludeChain += mapToProp.Name;

                        //if this is the primary type, then use the specified property's name.
                        if (selectorProp.DeclaringType.Equals(parentType)
                            || (parentType.IsGenericType && parentType.GenericTypeArguments[0].Equals(selectorProp.DeclaringType)))
                        {
                            query = (query as dynamic).Include(newIncludeChain);
                            query = IncludeNestedEntityProperties(query, eagerLoad, selectorProp.PropertyType, profileName, currentRecursionLevel, newIncludeChain);
                        }
                    }
                }
            }
            return query;
        }

        /// <summary>
        /// Returns the mapping counterpart to a given selector.
        /// </summary>
        /// <param name="selector">Property to find the counterpart for.</param>
        /// <param name="profile">Profile to search in (Empty: Default).</param>
        /// <returns>Returns the mapping counterpart to a given PropertyInfo.</returns>
        public static PropertyInfo GetMappedCounterPart<T>(Expression<Func<T, object>> selector, string profile = DEFAULT_PROFILE_NAME)
        {
            PropertyInfo selectorProp = GetSelectorPropertyInfo(selector);
            return GetMappedCounterPart(selectorProp, profile);
        }

        /// <summary>
        /// Returns the mapping counterpart to a given PropertyInfo.
        /// </summary>
        /// <param name="property">Property to find the counterpart for.</param>
        /// <param name="profile">Profile to search in (Empty: Default).</param>
        /// <returns>Returns the mapping counterpart to a given PropertyInfo.</returns>
        public static PropertyInfo GetMappedCounterPart(PropertyInfo property, string profile = DEFAULT_PROFILE_NAME)
        {
            PropertyInfo mapToProp = null;
            if (Mappings.Keys.Contains(profile))
            {
                var profileMappings = Mappings[profile];

                Mapping mapping = null;
                //now that we have the selector property, get the mapped property counterpart.
                foreach (var item in profileMappings)
                {
                    if (item.Contains(property))
                    {
                        mapping = item;
                        break;
                    }
                }

                if (mapping != null)
                {
                    mapToProp = mapping.Entity1.DeclaringType.Equals(property.DeclaringType) && mapping.Entity1.Name.Equals(property.Name) ?
                        mapping.Entity2 : mapping.Entity1;
                }
            }
            return mapToProp;
        }

        /// <summary>
        /// Removes an entire profile from the mappings.
        /// </summary>
        /// <param name="profile">Profile name to remove.</param>
        public static void RemoveProfile(string profile)
        {
            if (Mappings.Keys.Contains(profile))
            {
                Mappings.Remove(profile);
            }
        }

        /// <summary>
        /// Returns true if the passed selector exists in the passed profile's mappings.
        /// </summary>
        /// <typeparam name="T">Type of selector class.</typeparam>
        /// <param name="selector">Property selector of T.</param>
        /// <param name="profile">Mapping profile to search.  Empty: Default.</param>
        /// <returns>Returns true if the passed selector exists in the passed profile's mappings.</returns>
        public static bool Contains<T>(Expression<Func<T, object>> selector, string profile = DEFAULT_PROFILE_NAME)
        {
            PropertyInfo selectorProp = GetSelectorPropertyInfo(selector);
            return Contains(selectorProp, profile);
        }

        /// <summary>
        /// Returns true if the passed selector exists in the passed profile's mappings.
        /// </summary>
        /// <param name="property">Property to search for in the passed profile's mappings.</param>
        /// <param name="profile">Mapping profile to search.  Empty: Default.</param>
        /// <returns>Returns true if the passed selector exists in the passed profile's mappings.</returns>
        public static bool Contains(PropertyInfo property, string profile = DEFAULT_PROFILE_NAME)
        {
            if (Mappings.Keys.Contains(profile))
            {
                var profileMappings = Mappings[profile];


                Mapping mapping = null;
                foreach (var item in profileMappings)
                {
                    if (item.Contains(property))
                    {
                        mapping = item;
                        break;
                    }
                }

                return mapping != null;
            }

            return false;
        }
    }
}
