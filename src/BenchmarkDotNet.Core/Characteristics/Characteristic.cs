﻿using System;
using System.Linq.Expressions;

using static BenchmarkDotNet.Characteristics.CharacteristicHelper;

namespace BenchmarkDotNet.Characteristics
{
    public abstract class Characteristic
    {
        public static readonly object EmptyValue = new object();

        #region Factory methods
        public static Characteristic<T> Create<TOwner, T>(
            Expression<Func<TOwner, T>> propertyGetterExpression)
            where TOwner : JobMode =>
            new Characteristic<T>(
                GetMemberName(propertyGetterExpression),
                GetDeclaringType(propertyGetterExpression),
                null, default(T),
                false);

        public static Characteristic<T> Create<TOwner, T>(
            Expression<Func<TOwner, T>> propertyGetterExpression,
            T fallbackValue)
            where TOwner : JobMode =>
            new Characteristic<T>(
                GetMemberName(propertyGetterExpression),
                GetDeclaringType(propertyGetterExpression),
                null, fallbackValue,
                false);

        public static Characteristic<T> Create<TOwner, T>(
            Expression<Func<TOwner, T>> propertyGetterExpression,
            Func<JobMode, T, T> resolver,
            T fallbackValue)
            where TOwner : JobMode =>
            new Characteristic<T>(
                GetMemberName(propertyGetterExpression),
                GetDeclaringType(propertyGetterExpression),
                resolver, fallbackValue,
                false);

        public static Characteristic<T> Create<TOwner, T>(
            Expression<Func<TOwner, T>> propertyGetterExpression,
            Func<JobMode, T, T> resolver,
            T fallbackValue,
            bool dontClone)
            where TOwner : JobMode =>
            new Characteristic<T>(
                GetMemberName(propertyGetterExpression),
                GetDeclaringType(propertyGetterExpression),
                resolver, fallbackValue,
                dontClone);
        #endregion

        protected Characteristic(
            string id,
            Type characteristicType,
            Type declaringType,
            object fallbackValue,
            bool dontClone)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));
            if (characteristicType == null)
                throw new ArgumentNullException(nameof(characteristicType));
            if (declaringType == null)
                throw new ArgumentNullException(nameof(declaringType));

            Id = id;
            CharacteristicType = characteristicType;
            DeclaringType = declaringType;
            FallbackValue = fallbackValue;
            DontClone = dontClone;
        }

        public string Id { get; }
        public string FullId => DeclaringType.Name + "." + Id;

        // TODO: better naming. Ignorable, DontApply, smth else?
        public bool DontClone { get; }

        public Type CharacteristicType { get; }

        public Type DeclaringType { get; }

        public object FallbackValue { get; }

        public object this[JobMode obj]
        {
            get { return obj.GetValue(this); }
            set { obj.SetValue(this, value); }
        }

        public bool HasChildCharacteristics => IsJobModeSubclass(CharacteristicType);

        internal virtual object ResolveValueCore(JobMode obj, object currentValue) =>
            ReferenceEquals(currentValue, EmptyValue) ? FallbackValue : currentValue;

        public override string ToString() => Id;
    }
}