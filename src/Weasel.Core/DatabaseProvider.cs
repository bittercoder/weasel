using System;
using System.Data.Common;
using System.Linq;
using Baseline.ImTools;

namespace Weasel.Core
{
    public interface IDatabaseProvider<TCommand, TParameter, TConnection, TTransaction, TParameterType, TDataReader>
        where TCommand : DbCommand, new()
        where TParameter : DbParameter
        where TConnection : DbConnection
        where TTransaction : DbTransaction
        where TDataReader : DbDataReader
        where TParameterType : struct
    {
        TParameterType? TryGetDbType(Type? type);
        TParameterType ToParameterType(Type type);
        Type[] ResolveTypes(TParameterType parameterType);
        string GetDatabaseType(Type memberType, EnumStorage enumStyle);


        void AddParameter(TCommand command, TParameter parameter);
        void SetParameterType(TParameter parameter, TParameterType dbType);
        
        TParameterType StringParameterType { get; }
        TParameterType IntegerParameterType { get; }
        TParameterType LongParameterType { get; }
        TParameterType GuidParameterType { get; }
        TParameterType BoolParameterType { get; }
        TParameterType DoubleParameterType { get; }
    }

    public abstract class DatabaseProvider<TCommand, TParameter, TConnection, TTransaction, TParameterType, TDataReader>
        : IDatabaseProvider<TCommand, TParameter, TConnection, TTransaction, TParameterType, TDataReader>
        where TCommand : DbCommand, new()
        where TParameter : DbParameter
        where TConnection : DbConnection
        where TTransaction : DbTransaction
        where TDataReader : DbDataReader
        where TParameterType : struct  
    {
        protected readonly Ref<ImHashMap<Type, string>> DatabaseTypeMemo = Ref.Of(ImHashMap<Type, string>.Empty);
        protected readonly Ref<ImHashMap<Type, TParameterType?>> ParameterTypeMemo = Ref.Of(ImHashMap<Type, TParameterType?>.Empty);
        protected readonly Ref<ImHashMap<TParameterType, Type[]>> TypeMemo = Ref.Of(ImHashMap<TParameterType, Type[]>.Empty);

        protected DatabaseProvider()
        {
            StringParameterType = ToParameterType(typeof(string));
            IntegerParameterType = ToParameterType(typeof(int));
            LongParameterType = ToParameterType(typeof(long));
            GuidParameterType = ToParameterType(typeof(Guid));
            BoolParameterType = ToParameterType(typeof(bool));
            DoubleParameterType = ToParameterType(typeof(double));
        }

        protected abstract bool determineNpgsqlDbType(Type type, out TParameterType dbType);
        
        public TParameterType? TryGetDbType(Type? type)
        {
            if (type == null || !determineNpgsqlDbType(type, out var dbType))
                return null;

            return dbType;
        }
        
        public TParameterType ToParameterType(Type type)
        {
            if (determineNpgsqlDbType(type, out var dbType))
                return dbType;

            throw new NotSupportedException($"Can't infer {typeof(TParameterType).Name} for type " + type);
        }
        
        public void RegisterMapping(Type type, string databaseType, TParameterType? parameterType)
        {
            DatabaseTypeMemo.Swap(d => d.AddOrUpdate(type, databaseType));
            ParameterTypeMemo.Swap(d => d.AddOrUpdate(type, parameterType));
        }

        protected abstract Type[] determineClrTypesForParameterType(TParameterType dbType);

        public Type[] ResolveTypes(TParameterType parameterType)
        {
            if (TypeMemo.Value.TryFind(parameterType, out var values))
                return values;

            values = determineClrTypesForParameterType(parameterType);

            TypeMemo.Swap(d => d.AddOrUpdate(parameterType, values!));

            return values!;
        }

        public abstract string GetDatabaseType(Type memberType, EnumStorage enumStyle);


        public abstract void AddParameter(TCommand command, TParameter parameter);

        public abstract void SetParameterType(TParameter parameter, TParameterType dbType);


        public TParameterType StringParameterType { get; }
        public TParameterType IntegerParameterType { get; }
        public TParameterType LongParameterType { get; }
        public TParameterType GuidParameterType { get; }
        public TParameterType BoolParameterType { get; }
        public TParameterType DoubleParameterType { get; }
        
        public TParameter AddParameter(TCommand command, object value, TParameterType? dbType = null)
        {
            var name = "p" + command.Parameters.Count;

            var parameter = (TParameter)command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;

            if (dbType.HasValue)
            {
                SetParameterType(parameter, dbType.Value);
            }

            command.Parameters.Add(parameter);

            return parameter;
        }
        
        /// <summary>
        /// Finds or adds a new parameter with the specified name and returns the parameter
        /// </summary>
        /// <param name="command"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public TParameter AddNamedParameter(TCommand command, string name, object value, TParameterType? dbType = null)
        {
            var existing = command.Parameters.OfType<TParameter>().FirstOrDefault(x => x.ParameterName == name);
            if (existing != null) return existing;

            var parameter = (TParameter)command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;


            if (dbType.HasValue)
            {
                SetParameterType(parameter, dbType.Value);
            }
            else if (value != null)
            {
                dbType = TryGetDbType(value.GetType());
                SetParameterType(parameter, dbType.Value);
            }
            
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);

            return parameter;
        }

        public TParameter AddNamedParameter(TCommand command, string name, string value)
        {
            return AddNamedParameter(command, name, value, StringParameterType);
        }
        
        public TParameter AddNamedParameter(TCommand command, string name, int value)
        {
            return AddNamedParameter(command, name, value, IntegerParameterType);
        }
        
        public TParameter AddNamedParameter(TCommand command, string name, long value)
        {
            return AddNamedParameter(command, name, value, LongParameterType);
        }
        
        public TParameter AddNamedParameter(TCommand command, string name, double value)
        {
            return AddNamedParameter(command, name, value, DoubleParameterType);
        }
        
        public TParameter AddNamedParameter(TCommand command, string name, Guid value)
        {
            return AddNamedParameter(command, name, value, GuidParameterType);
        }
        
        public TParameter AddNamedParameter(TCommand command, string name, bool value)
        {
            return AddNamedParameter(command, name, value, BoolParameterType);
        }
        
        
        
        
        

    }
}