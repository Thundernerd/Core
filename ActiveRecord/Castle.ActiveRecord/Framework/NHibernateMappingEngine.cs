// Copyright 2004-2005 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Castle.ActiveRecord
{
	using System;
	using System.Collections;
	using System.Configuration;
	using System.Reflection;
	using System.Text;

	/// <summary>
	/// 
	/// </summary>
	public class NHibernateMappingEngine
	{
		private const BindingFlags PropertiesBindingFlags = BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.Instance;
		private static readonly String subclassOpen = "\r\n<subclass name=\"{0}\" {1}>";
		private static readonly String subclassClose = "\r\n</subclass>";
		private static readonly String joinedsubclassOpen = "\r\n<joined-subclass name=\"{0}\" {1}>";
		private static readonly String joinedsubclassClose = "\r\n</joined-subclass>";
		private static readonly String keyElement = "<key column=\"{0}\"/>";
		private static readonly String mappingOpen = "\r\n<hibernate-mapping xmlns=\"urn:nhibernate-mapping-2.0\" {0}>";
		private static readonly String mappingClose = "\r\n</hibernate-mapping>";
		private static readonly String classOpen = "\r\n<class name=\"{0}\" {1}>";
		private static readonly String classClose = "\r\n</class>";
		private static readonly String discValueAttribute = "discriminator-value=\"{0}\" ";
		private static readonly String tableAttribute = "table=\"{0}\" ";
		private static readonly String proxyAttribute = "proxy=\"{0}\" ";
		private static readonly String schemaAttribute = "schema=\"{0}\" ";
		private static readonly String idOpen = "\r\n<id {0}>";
		private static readonly String collectionIdOpen = "\r\n<collection-id {0}>";
		private static readonly String collectionIdClose = "\r\n</collection-id>";
		private static readonly String idClose = "\r\n</id>";
		private static readonly String nameAttribute = "name=\"{0}\" ";
		private static readonly String typeAttribute = "type=\"{0}\" ";
		private static readonly String classAttribute = "class=\"{0}\" ";
		private static readonly String generatorOpen = "\r\n<generator class=\"{0}\">";
		private static readonly String generatorClose = "\r\n</generator>";
		private static readonly String propertyOpen = "\r\n<property {0} {1}>";
		private static readonly String propertyClose = "\r\n</property>";
		private static readonly String updateAttribute = "update=\"{0}\" ";
		private static readonly String insertAttribute = "insert=\"{0}\" ";
		private static readonly String formulaAttribute = "formula=\"{0}\" ";
		private static readonly String columnAttribute = "column=\"{0}\" ";
		private static readonly String lengthAttribute = "length=\"{0}\" ";
		private static readonly String notNullAttribute = "not-null=\"{0}\" ";
		private static readonly String oneToOne = "\r\n<one-to-one name=\"{0}\" {1} {2}/>";
		private static readonly String oneToMany = "\r\n<one-to-many class=\"{0}\" />";
		private static readonly String manyToOne = "\r\n<many-to-one {0} {1} />";
		private static readonly String manyToMany = "\r\n<many-to-many class=\"{0}\" {1} />";
		private static readonly String cascadeAttribute = "cascade=\"{0}\" ";
		private static readonly String outerJoinAttribute = "outer-join=\"{0}\" ";
//		private static readonly String accessAttribute = "access=\"{0}\" ";
		private static readonly String unsavedValueAttribute = "unsaved-value=\"{0}\" ";
		private static readonly String constrainedAttribute = "constrained=\"{0}\" ";
		private static readonly String mapOpen = "\r\n<map name=\"{0}\" {1}>";
		private static readonly String mapClose = "\r\n</map>";
		private static readonly String listOpen = "\r\n<list name=\"{0}\" {1}>";
		private static readonly String listClose = "\r\n</list>";
		private static readonly String setOpen = "\r\n<set name=\"{0}\" {1}>";
		private static readonly String idbagOpen = "\r\n<idbag name=\"{0}\" {1}>";
		private static readonly String idbagClose = "\r\n</idbag>";
		private static readonly String setClose = "\r\n</set>";
		private static readonly String bagOpen = "\r\n<bag name=\"{0}\" {1}>";
		private static readonly String bagClose = "\r\n</bag>";
		private static readonly String keyTag = "\r\n<key column=\"{0}\"/>";
		private static readonly String elementTag = "\r\n<element column=\"{0}\" class=\"{1}\"/>";
		private static readonly String indexTag = "\r\n<index column=\"{0}\" {1} />";
		private static readonly String lazyAttribute = "lazy=\"{0}\" ";
		private static readonly String inverseAttribute = "inverse=\"{0}\" ";
		private static readonly String orderByAttribute = "order-by=\"{0}\" ";
		private static readonly String whereAttribute = "where=\"{0}\" ";
		private static readonly String componentOpen = "\r\n<component {0} {1}>";
		private static readonly String componentClose = "\r\n</component>";
		private static readonly String paramElement = "\r\n<param name=\"{0}\">{1}</param>";

		private IList _visited = new ArrayList();
		
		public String CreateMapping(Type type, Type[] sefOfTypes)
		{
			if (_visited.Contains(type)) return String.Empty;
			
			_visited.Add(type);

			if (!type.IsDefined(typeof(ActiveRecordAttribute), true))
			{
				return String.Empty;
			}

			ActiveRecordAttribute ar = GetActiveRecord(type);

			if (ar == null)
			{
				return String.Empty;
			}

				// Is it a child?

				if (IsChildActiveRecord(ar, type))
				{
					// In this case, it must be mapped as a subclass later
					
					_visited.Remove(type);

					return String.Empty;
				}

				StringBuilder xml = new StringBuilder(String.Format(mappingOpen, ""));

				String table = (ar.Table == null ? "" : String.Format(tableAttribute, ar.Table));
				String schema = (ar.Schema == null ? "" : String.Format(schemaAttribute, ar.Schema));
				String proxy = (ar.Proxy == false ? "" : String.Format(proxyAttribute, ar.Proxy.ToString().ToLower()));
				String disc = (ar.DiscriminatorValue == null ? "" : String.Format(discValueAttribute, ar.DiscriminatorValue));

				xml.AppendFormat(classOpen, GetNHibernateName( type ), table + schema + proxy + disc);

				AddMappedIdOrCompositeId(xml, type.GetProperties( PropertiesBindingFlags ));

				if (AddDiscrimitator(xml, ar))
				{
					AddMappedProperties(xml, type.GetProperties( PropertiesBindingFlags ));
					AddSubClasses(xml, ar, type, sefOfTypes);
				}
				else if (type.IsDefined( typeof(JoinedBaseAttribute), false ))
				{
					AddMappedProperties(xml, type.GetProperties( PropertiesBindingFlags ));
					AddJoinedSubClasses(xml, ar, type, sefOfTypes);
				}
				else
				{
					AddMappedProperties(xml, type.GetProperties( PropertiesBindingFlags ));
				}

				xml.Append(classClose).Append(mappingClose);

				return xml.ToString();
		}

		/// <summary>
		/// Is it a SubClass or Joined Subclass?
		/// </summary>
		private bool IsChildActiveRecord(ActiveRecordAttribute ar, Type type)
		{
			return (ar.DiscriminatorValue != null && ar.DiscriminatorColumn == null) || GetPropertyWithAttribute(type, typeof(KeyAttribute)) != null;
		}

		private void CreateSubClassMapping(StringBuilder xml, Type type, Type[] sefOfTypes)
		{
			_visited.Add(type);

			ActiveRecordAttribute ar = GetActiveRecord(type);

			if (ar != null)
			{
				String proxy = (ar.Proxy == false ? "" : String.Format(proxyAttribute, ar.Proxy.ToString().ToLower()));
				String discvalue = (ar.DiscriminatorValue == null ? "" : String.Format(discValueAttribute, ar.DiscriminatorValue));

				xml.AppendFormat(subclassOpen, GetNHibernateName( type ), proxy + discvalue);

				AddMappedProperties(xml, type.GetProperties( PropertiesBindingFlags ));

				xml.Append(subclassClose);
			}
		}

		private void CreateJoinedSubClassMapping(StringBuilder xml, Type type, Type[] sefOfTypes)
		{
			_visited.Add(type);

			ActiveRecordAttribute ar = GetActiveRecord(type);

			if (ar != null)
			{
				String table = (ar.Table == null ? "" : String.Format(tableAttribute, ar.Table));
				String proxy = (ar.Proxy == false ? "" : String.Format(proxyAttribute, ar.Proxy.ToString().ToLower()));

				xml.AppendFormat(joinedsubclassOpen, GetNHibernateName( type ), table + proxy);

				PropertyInfo keyProp = GetPropertyWithAttribute(type, typeof(KeyAttribute));

				KeyAttribute keyAtt = null;

				if (keyProp != null)
				{
					object[] attrs = keyProp.GetCustomAttributes(typeof(KeyAttribute), false);
					keyAtt = attrs[0] as KeyAttribute;
				}

				if (keyAtt == null)
				{
					String message = String.Format("The type {0} extends another class " + 
						"and bounds itself to a different table, which implies it's a joined subclass. " + 
						"However it does not defines a key property. Use the KeyAttribute", type.FullName);
					
					throw new ConfigurationException(message);
				}

				String columnKey = keyAtt.ColumnName == null ? keyProp.Name : keyAtt.ColumnName;

				xml.AppendFormat(keyElement, columnKey);

				if (type.IsDefined( typeof(JoinedBaseAttribute), false ))
				{
					AddMappedProperties(xml, type.GetProperties( PropertiesBindingFlags ));
					AddJoinedSubClasses(xml, ar, type, sefOfTypes);
				}
				else
				{
					AddMappedProperties(xml, type.GetProperties( PropertiesBindingFlags ));
				}


				xml.Append(joinedsubclassClose);
			}
		}

		private void AddMappedIdOrCompositeId(StringBuilder builder, PropertyInfo[] props)
		{
			// TODO: Composite-id

			foreach (PropertyInfo prop in props)
			{
				object[] attributes = prop.GetCustomAttributes(false);
				foreach (object attribute in attributes)
				{
					PrimaryKeyAttribute pk = attribute as PrimaryKeyAttribute;
					if (pk != null)
					{
						AddPrimaryKeyMapping(prop, pk, builder);

						continue;
					}
				}
			}
		}

		private void AddMappedProperties(StringBuilder builder, PropertyInfo[] props)
		{
			foreach (PropertyInfo prop in props)
			{
				object[] attributes = prop.GetCustomAttributes(false);

				foreach (object attribute in attributes)
				{
					PropertyAttribute property = attribute as PropertyAttribute;
					if (property != null)
					{
						AddPropertyMapping(property, prop, builder);

						continue;
					}

					NestedAttribute nested = attribute as NestedAttribute;
					if (nested != null)
					{
						AddComponentMapping(prop, builder);

						continue;
					}

					BelongsToAttribute belongs = attribute as BelongsToAttribute;
					if (belongs != null)
					{
						AddManyToOneMapping(prop, belongs, builder);

						continue;
					}
					HasAndBelongsToManyAttribute hasAndBelongs = attribute as HasAndBelongsToManyAttribute;
					if (hasAndBelongs != null)
					{
						if (hasAndBelongs.RelationType == RelationType.Bag)
						{
							AddBagMapping(prop, hasAndBelongs, builder);
						}
//						else if (hasmany.RelationType == RelationType.Map)
//						{
//							AddMapMapping(prop, hasmany, builder);
//						}
//						else if (hasmany.RelationType == RelationType.List)
//						{
//							AddListMapping(prop, hasmany, builder);
//						}
						else if (hasAndBelongs.RelationType == RelationType.IdBag)
						{
							CollectionIDAttribute collectionID = GetCollectionIDAttribute(attributes);
							if (collectionID == null)
								throw new NullReferenceException("Collection-id attribute required for idbag mapping");
							AddIDBagMapping(prop, hasAndBelongs, collectionID, builder);
						}
						else if (hasAndBelongs.RelationType == RelationType.Set)
						{
							AddSetMapping(prop, hasAndBelongs, builder);
						}
						else
						{
							String message = String.Format("Sorry but we do not support " + 
								"mapping of '{0}' yet", hasAndBelongs.RelationType);
							throw new NotSupportedException(message);
						}

						continue;
					}

					HasManyAttribute hasmany = attribute as HasManyAttribute;
					if (hasmany != null)
					{
						// TODO: Inspect the return type to infer the 
						// mapping type

						if (hasmany.RelationType == RelationType.Bag)
						{
							AddBagMapping(prop, hasmany, builder);
						}
						else if (hasmany.RelationType == RelationType.Map)
						{
							AddMapMapping(prop, hasmany, builder);
						}
						else if (hasmany.RelationType == RelationType.List)
						{
							AddListMapping(prop, hasmany, builder);
						}
						else if (hasmany.RelationType == RelationType.Set)
						{
							AddSetMapping(prop, hasmany, builder);
						}
						else
						{
							String message = String.Format("Sorry but we do not support " + 
								"mapping of '{0}' yet", hasmany.RelationType);
							throw new NotSupportedException(message);
						}

						continue;
					}

					HasOneAttribute hasone = attribute as HasOneAttribute;
					if (hasone != null)
					{
//						Type otherType = prop.PropertyType;

						AddOneToOneMapping(prop, hasone, builder);

//						object[] otherAttributes = otherType.GetCustomAttributes(typeof (BelongsToAttribute), false);
//						BelongsToAttribute inverse = null;
//						foreach (object o in otherAttributes)
//						{
//							if (o is BelongsToAttribute)
//							{
//								inverse = o as BelongsToAttribute;
//								break;
//							}
//						}
//						if (inverse != null && inverse.Type == prop.DeclaringType)
//						{
//							AddOneToOneMapping(prop, hasone, builder);
//						}
						// throw exception if no BelongsToAttribute?
					}
				}
			}
		}

		private static CollectionIDAttribute GetCollectionIDAttribute(object[] attributes)
		{
			CollectionIDAttribute collectionID = null;
			foreach (object collectionid in attributes)
				if (collectionid is CollectionIDAttribute && (collectionID = (CollectionIDAttribute) collectionid) != null) break;
			return collectionID;
		}

		private void AddComponentMapping(PropertyInfo prop, StringBuilder builder)
		{
			String name = String.Format(nameAttribute, prop.Name);
			String klass = String.Format(classAttribute, prop.PropertyType.AssemblyQualifiedName);
			builder.AppendFormat(componentOpen, name, klass);
			AddMappedProperties(builder, prop.PropertyType.GetProperties( PropertiesBindingFlags ));
			builder.AppendFormat(componentClose);
		}

		private void AddOneToOneMapping(PropertyInfo prop, HasOneAttribute hasone, StringBuilder builder)
		{
			String name = prop.Name;
			String klass = String.Format(classAttribute, prop.PropertyType.AssemblyQualifiedName);
			String cascade = (hasone.Cascade == null ? "" : String.Format(cascadeAttribute, hasone.Cascade));
			String outer = (hasone.OuterJoin == null ? "" : String.Format(outerJoinAttribute, hasone.OuterJoin));
			String constrained = (hasone.Constrained == null ? "" : String.Format(constrainedAttribute, hasone.Constrained));
			builder.AppendFormat(oneToOne, name, klass, cascade + outer + constrained);
		}
		
	  private void AddSetMapping(PropertyInfo prop, HasAndBelongsToManyAttribute hasAndBelongsTo, StringBuilder builder)
	  {
		String name = prop.Name;
		String table = ( hasAndBelongsTo.Table == null ? "" : String.Format(tableAttribute, hasAndBelongsTo.Table) );
		String schema = ( hasAndBelongsTo.Schema == null ? "" : String.Format(schemaAttribute, hasAndBelongsTo.Schema) );
		String lazy = ( hasAndBelongsTo.Lazy == false ? "" : String.Format(lazyAttribute, hasAndBelongsTo.Lazy.ToString().ToLower()) );
		String inverse = ( hasAndBelongsTo.Inverse == false ? "" : String.Format(inverseAttribute, hasAndBelongsTo.Inverse.ToString().ToLower()) );
		String cascade = ( hasAndBelongsTo.Cascade == null ? "" : String.Format(cascadeAttribute, hasAndBelongsTo.Cascade) );
		//			String sort = (hasAndBelongsTo.Sort == null ? "" : String.Format(sortAttribute, hasAndBelongsTo.Sort));
		String orderBy = ( hasAndBelongsTo.OrderBy == null ? "" : String.Format(orderByAttribute, hasAndBelongsTo.OrderBy) );
		String where = ( hasAndBelongsTo.Where == null ? "" : String.Format(whereAttribute, hasAndBelongsTo.Where) );

		builder.AppendFormat(setOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);

		Type otherType = hasAndBelongsTo.MapType;
		String columnkey = hasAndBelongsTo.ColumnKey == null ? "" : hasAndBelongsTo.ColumnKey;
		String column = hasAndBelongsTo.Column == null ? "" : String.Format(columnAttribute, hasAndBelongsTo.Column);

		builder.AppendFormat(keyTag, columnkey);

		// We need to choose from element, one-to-many, many-to-many, composite-element, many-to-any
		// We need to do it wisely
		if (column != null)
		{
		  builder.AppendFormat(manyToMany, otherType.AssemblyQualifiedName, column);
		}

		builder.Append(setClose);
	  }


		private void AddSetMapping(PropertyInfo prop, HasManyAttribute hasmany, StringBuilder builder)
		{
			String name = prop.Name;
			String table = (hasmany.Table == null ? "" : String.Format(tableAttribute, hasmany.Table));
			String schema = (hasmany.Schema == null ? "" : String.Format(schemaAttribute, hasmany.Schema));
			String lazy = (hasmany.Lazy == false ? "" : String.Format(lazyAttribute, hasmany.Lazy.ToString().ToLower()));
			String inverse = (hasmany.Inverse == false ? "" : String.Format(inverseAttribute, hasmany.Inverse.ToString().ToLower()));
			String cascade = (hasmany.Cascade == null ? "" : String.Format(cascadeAttribute, hasmany.Cascade));
//			String sort = (hasmany.Sort == null ? "" : String.Format(sortAttribute, hasmany.Sort));
			String orderBy = (hasmany.OrderBy == null ? "" : String.Format(orderByAttribute, hasmany.OrderBy));
			String where = (hasmany.Where == null ? "" : String.Format(whereAttribute, hasmany.Where));

			builder.AppendFormat(setOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);

			Type otherType = hasmany.MapType;
			if (hasmany.Key != null)
			{
				PropertyInfo elementProp = otherType.GetProperty(hasmany.Key);
				if (elementProp != null)
				{
					builder.AppendFormat(keyTag, hasmany.Key);
					PropertyInfo indexProp = otherType.GetProperty(hasmany.Index);
					if (indexProp != null)
					{
						String type = String.Format(typeAttribute, indexProp.Name);
						builder.AppendFormat(indexTag, hasmany.Index, type);
					}
					String column = null;
					object[] elementAttributes = elementProp.GetCustomAttributes(false);
					foreach (object attribute in elementAttributes)
					{
						if (attribute is PropertyAttribute)
						{
							column = (attribute as PropertyAttribute).Column;
						}
						else if (attribute is PrimaryKeyAttribute)
						{
							column = (attribute as PrimaryKeyAttribute).Column;
						}
						else if (attribute is BelongsToAttribute)
						{
							column = (attribute as BelongsToAttribute).Column;
						}
					}
					if (column != null)
					{
						builder.AppendFormat(elementTag, column, otherType.Name);
					}
				}
			}
			builder.Append(setClose);
		}

		private void AddListMapping(PropertyInfo prop, HasManyAttribute hasmany, StringBuilder builder)
		{
			String name = prop.Name;
			String table = (hasmany.Table == null ? "" : String.Format(tableAttribute, hasmany.Table));
			String schema = (hasmany.Schema == null ? "" : String.Format(schemaAttribute, hasmany.Schema));
			String lazy = (hasmany.Lazy == false ? "" : String.Format(lazyAttribute, hasmany.Lazy.ToString().ToLower()));
			String inverse = (hasmany.Inverse == false ? "" : String.Format(inverseAttribute, hasmany.Inverse.ToString().ToLower()));
			String cascade = (hasmany.Cascade == null ? "" : String.Format(cascadeAttribute, hasmany.Cascade));
//			String sort = (hasmany.Sort == null ? "" : String.Format(sortAttribute, hasmany.Sort));
			String orderBy = (hasmany.OrderBy == null ? "" : String.Format(orderByAttribute, hasmany.OrderBy));
			String where = (hasmany.Where == null ? "" : String.Format(whereAttribute, hasmany.Where));

			builder.AppendFormat(listOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);

			Type otherType = hasmany.MapType;
			
			PropertyInfo indexProp = otherType.GetProperty(hasmany.Index);
			
			if (indexProp != null)
			{
				String type = String.Format(typeAttribute, indexProp.Name);
				builder.AppendFormat(indexTag, hasmany.Index, type);

				String column = null;
				
				object[] elementAttributes = indexProp.GetCustomAttributes(false);
				
				foreach (object attribute in elementAttributes)
				{
					if (attribute is PropertyAttribute)
					{
						column = (attribute as PropertyAttribute).Column;
					}
					else if (attribute is PrimaryKeyAttribute)
					{
						column = (attribute as PrimaryKeyAttribute).Column;
					}
					else if (attribute is BelongsToAttribute)
					{
						column = (attribute as BelongsToAttribute).Column;
					}
				}
				if (column != null)
				{
					builder.AppendFormat(elementTag, column, otherType.Name);
				}
			}
			builder.Append(listClose);
		}

		private void AddMapMapping(PropertyInfo prop, HasManyAttribute hasmany, StringBuilder builder)
		{
			String name = prop.Name;
			String table = (hasmany.Table == null ? "" : String.Format(tableAttribute, hasmany.Table));
			String schema = (hasmany.Schema == null ? "" : String.Format(schemaAttribute, hasmany.Schema));
			String lazy = (hasmany.Lazy == false ? "" : String.Format(lazyAttribute, hasmany.Lazy.ToString().ToLower()));
			String inverse = (hasmany.Inverse == false ? "" : String.Format(inverseAttribute, hasmany.Inverse.ToString().ToLower()));
			String cascade = (hasmany.Cascade == null ? "" : String.Format(cascadeAttribute, hasmany.Cascade));
//			String sort = (hasmany.Sort == null ? "" : String.Format(sortAttribute, hasmany.Sort));
			String orderBy = (hasmany.OrderBy == null ? "" : String.Format(orderByAttribute, hasmany.OrderBy));
			String where = (hasmany.Where == null ? "" : String.Format(whereAttribute, hasmany.Where));

			builder.AppendFormat(mapOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);

			Type otherType = hasmany.MapType;
			PropertyInfo elementProp = otherType.GetProperty(hasmany.Key);
			if (elementProp != null)
			{
				builder.AppendFormat(keyTag, hasmany.Key);
				String column = null;
				object[] elementAttributes = elementProp.GetCustomAttributes(false);
				foreach (object attribute in elementAttributes)
				{
					if (attribute is PropertyAttribute)
					{
						column = (attribute as PropertyAttribute).Column;
					}
					else if (attribute is PrimaryKeyAttribute)
					{
						column = (attribute as PrimaryKeyAttribute).Column;
					}
					else if (attribute is BelongsToAttribute)
					{
						column = (attribute as BelongsToAttribute).Column;
					}
				}
				if (column != null)
				{
					builder.AppendFormat(elementTag, column, otherType.Name);
				}
			}
			builder.Append(mapClose);
		}

		private void AddManyToOneMapping(PropertyInfo prop, BelongsToAttribute belongs, StringBuilder builder)
		{
			String name = String.Format(nameAttribute, prop.Name);
			String klass = String.Format(classAttribute, GetNHibernateName( prop.PropertyType ) );
			String column = (belongs.Column == null ? "" : String.Format(columnAttribute, belongs.Column));
			String cascade = (belongs.Cascade == null ? "" : String.Format(cascadeAttribute, belongs.Cascade));
			String outer = (belongs.OuterJoin == null ? "" : String.Format(outerJoinAttribute, belongs.OuterJoin));
			String update = (belongs.Update == null ? "" : String.Format(updateAttribute, belongs.Update));
			String insert = (belongs.Insert == null ? "" : String.Format(insertAttribute, belongs.Insert));

			builder.AppendFormat(manyToOne, name, klass + column + cascade + outer + update + insert);
		}

		private void AddPropertyMapping(PropertyAttribute property, PropertyInfo prop, StringBuilder builder)
		{
			String column = (property.Column == null ? String.Format(columnAttribute, prop.Name) : String.Format(columnAttribute, property.Column));
			String update = (property.Update == null ? "" : String.Format(updateAttribute, property.Update));
			String insert = (property.Insert == null ? "" : String.Format(insertAttribute, property.Insert));
			String formula = (property.Formula == null ? "" : String.Format(formulaAttribute, property.Formula));
			String length = (property.Length == 0 ? "" : String.Format(lengthAttribute, property.Length));
			String notNull = (property.NotNull == false ? "" : String.Format(notNullAttribute, property.NotNull.ToString().ToLower()));
			String name = String.Format(nameAttribute, prop.Name);
			String type = String.Format(typeAttribute, property.ColumnType != null ? property.ColumnType : prop.PropertyType.Name);

			builder.AppendFormat(propertyOpen, name, type + column + update + insert + formula + length + notNull);
			builder.Append(propertyClose);
		}

		private void AddPrimaryKeyMapping(PropertyInfo prop, PrimaryKeyAttribute pk, StringBuilder builder)
		{
			String name = String.Format(nameAttribute, prop.Name);
			String type = String.Format(typeAttribute, prop.PropertyType.Name);
			String column = (pk.Column == null ? String.Format(columnAttribute, prop.Name) : String.Format(columnAttribute, pk.Column));

			if (pk.UnsavedValue == null)
			{
				if (prop.PropertyType.IsPrimitive)
				{
					pk.UnsavedValue = "0";
				}
				else if (prop.PropertyType != typeof(Guid))
				{
					// Nasty guess, but for 99.98% of situations it will be OK
					pk.UnsavedValue = "";
				}
			}

			String unsavedValue = (pk.UnsavedValue == null ? "" : String.Format(unsavedValueAttribute, pk.UnsavedValue));

			builder.AppendFormat(idOpen, name + type + column + unsavedValue);

			if (pk.Generator != PrimaryKeyType.None)
			{
				builder.AppendFormat(generatorOpen, pk.Generator.ToString().ToLower());

				if (pk.Generator == PrimaryKeyType.Foreign)
				{
					PropertyInfo hasOneProp = GetPropertyWithAttribute( prop.DeclaringType, typeof(HasOneAttribute) );

					if (hasOneProp != null)
					{
						builder.AppendFormat("<param name=\"property\">{0}</param>\r\n", hasOneProp.Name);
					}
				}

				builder.Append(generatorClose);
			}

			builder.Append(idClose);
		}

		private ActiveRecordAttribute GetActiveRecord(MemberInfo klass)
		{
			foreach (Attribute attribute in klass.GetCustomAttributes(false))
			{
				if (attribute is ActiveRecordAttribute)
				{
					return attribute as ActiveRecordAttribute;
				}
			}
			return null;
		}

		private void AddBagMapping(PropertyInfo prop, HasAndBelongsToManyAttribute hasAndBelongsTo, StringBuilder builder)
		{
			String name = prop.Name;
			String table = (hasAndBelongsTo.Table == null ? "" : String.Format(tableAttribute, hasAndBelongsTo.Table));
			String schema = (hasAndBelongsTo.Schema == null ? "" : String.Format(schemaAttribute, hasAndBelongsTo.Schema));
			String lazy = (hasAndBelongsTo.Lazy == false ? "" : String.Format(lazyAttribute, hasAndBelongsTo.Lazy.ToString().ToLower()));
			String inverse = (hasAndBelongsTo.Inverse == false ? "" : String.Format(inverseAttribute, hasAndBelongsTo.Inverse.ToString().ToLower()));
			String cascade = (hasAndBelongsTo.Cascade == null ? "" : String.Format(cascadeAttribute, hasAndBelongsTo.Cascade));
			String orderBy = (hasAndBelongsTo.OrderBy == null ? "" : String.Format(orderByAttribute, hasAndBelongsTo.OrderBy));
			String where = (hasAndBelongsTo.Where == null ? "" : String.Format(whereAttribute, hasAndBelongsTo.Where));

			builder.AppendFormat(bagOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);

			Type otherType = hasAndBelongsTo.MapType;
			
			String columnkey = hasAndBelongsTo.ColumnKey == null ? "" : hasAndBelongsTo.ColumnKey;
			String column = hasAndBelongsTo.Column == null ? "" : String.Format(columnAttribute, hasAndBelongsTo.Column);

			builder.AppendFormat(keyTag, columnkey);

			// We need to choose from element, one-to-many, many-to-many, composite-element, many-to-any
			// We need to do it wisely
			if (column != null)
			{
				builder.AppendFormat(manyToMany, GetNHibernateName( otherType ), column);
			}
		
			builder.Append(bagClose);
		}

		private void AddBagMapping(PropertyInfo prop, HasManyAttribute hasmany, StringBuilder builder)
		{
			String name = prop.Name;
			String table = (hasmany.Table == null ? "" : String.Format(tableAttribute, hasmany.Table));
			String schema = (hasmany.Schema == null ? "" : String.Format(schemaAttribute, hasmany.Schema));
			String lazy = (hasmany.Lazy == false ? "" : String.Format(lazyAttribute, hasmany.Lazy.ToString().ToLower()));
			String inverse = (hasmany.Inverse == false ? "" : String.Format(inverseAttribute, hasmany.Inverse.ToString().ToLower()));
			String cascade = (hasmany.Cascade == null ? "" : String.Format(cascadeAttribute, hasmany.Cascade));
			String orderBy = (hasmany.OrderBy == null ? "" : String.Format(orderByAttribute, hasmany.OrderBy));
			String where = (hasmany.Where == null ? "" : String.Format(whereAttribute, hasmany.Where));

			Type otherType = hasmany.MapType;

			string key = "";
			if( otherType.IsSubclassOf( typeof( ActiveRecordBase ) ) )
			{
				PropertyInfo otherKey = GetPropertyWithAttribute( otherType, typeof( BelongsToAttribute ) );

				if (otherKey == null)
				{
					String message = String.Format("While mapping {0} we looked for a 'belongsto' association on {1} but haven't found it.", prop.DeclaringType.FullName, otherType.FullName);
					throw new ConfigurationException(message);
				}

				BelongsToAttribute belongs = GetBelongsToAttribute( otherKey );
				if( otherKey != null && belongs != null && otherKey.PropertyType == prop.DeclaringType )
				{
					key = belongs.Column;
				}
			}
			else
			{
				throw new ConfigurationException("Association with a class that does not extends ActiveRecordBase is invalid. Check " + otherType.FullName);
			}

			builder.AppendFormat(bagOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);
			builder.AppendFormat(keyTag, key);

			// We need to choose from element, one-to-many, many-to-many, composite-element, many-to-any
			// We need to do it wisely
			if (key.Length > 0)
			{
				builder.AppendFormat(oneToMany, GetNHibernateName( otherType ) );
			}
		
			builder.Append(bagClose);
		}

		private bool AddDiscrimitator(StringBuilder xml, ActiveRecordAttribute ar)
		{
			if (ar.DiscriminatorColumn != null)
			{
				xml.Append("\r\n\t<discriminator ");
				xml.Append( String.Format(" column=\"{0}\" ", ar.DiscriminatorColumn) );

				if (ar.DiscriminatorType != null)
				{
					xml.Append( String.Format(" type=\"{0}\" ", ar.DiscriminatorType) );
				}

				xml.Append("/>\r\n");
				
				return true;
			}

			return false;
		}

		private void AddSubClasses(StringBuilder xml, ActiveRecordAttribute ar, Type type, Type[] types)
		{
			foreach(Type sub in types)
			{
				if (sub.BaseType == type)
				{
					CreateSubClassMapping(xml, sub, types);
				}
			}
		}

		private void AddJoinedSubClasses(StringBuilder xml, ActiveRecordAttribute ar, Type type, Type[] types)
		{
			foreach(Type sub in types)
			{
				if (sub.BaseType == type)
				{
					ActiveRecordAttribute arsub = GetActiveRecord(sub);

					if (arsub == null)
					{
						String message = String.Format("Type {0} is a joined subclass, " + 
							"but does not even used the ActiveRecordAttribute to declare " + 
							"which table it belongs to.", sub.FullName);
						throw new ConfigurationException(message);
					}
					else if (arsub.Table == null)
					{
						String message = String.Format("Type {0} is a joined subclass, but " + 
							"does not declare which table it's mapping to.", sub.FullName);
						throw new ConfigurationException(message);
					}
					else if (arsub.Table.Equals(ar.Table))
					{
						String message = String.Format("Type {0} is a joined subclass, " + 
							"but it's pointing to the same table than its parent. In this " + 
							"case, you should use discriminator columns.", sub.FullName);
						throw new ConfigurationException(message);
					}

					CreateJoinedSubClassMapping(xml, sub, types);
				}
			}
		}

		private PropertyInfo GetPropertyWithAttribute(Type targetType, Type attributeType)
		{
			PropertyInfo[] props = targetType.GetProperties( BindingFlags.Instance|BindingFlags.Public );

			foreach(PropertyInfo prop in props)
			{
				if (prop.IsDefined(attributeType, true))
				{
					return prop;
				}
			}

			return null;
		}

		private BelongsToAttribute GetBelongsToAttribute( PropertyInfo prop )
		{
			object[] attributes = prop.GetCustomAttributes( true );
			foreach( object attribute in attributes )
			{
				if( attribute is BelongsToAttribute )
				{
					return attribute as BelongsToAttribute;
				}
			}
			return null;
		}

		private string GetNHibernateName( Type type )
		{
			return string.Format( "{0}, {1}", type.FullName, type.Assembly.GetName().Name );
		}

		private void AddIDBagMapping(PropertyInfo prop, HasAndBelongsToManyAttribute belongs, CollectionIDAttribute collection, StringBuilder builder)
		{
			String name = prop.Name;
			String table = ( belongs.Table == null ? "" : String.Format(tableAttribute, belongs.Table) );
			String schema = ( belongs.Schema == null ? "" : String.Format(schemaAttribute, belongs.Schema) );
			String lazy = ( belongs.Lazy == false ? "" : String.Format(lazyAttribute, belongs.Lazy.ToString().ToLower()) );
			String inverse = ( belongs.Inverse == false ? "" : String.Format(inverseAttribute, belongs.Inverse.ToString().ToLower()) );
			String cascade = ( belongs.Cascade == null ? "" : String.Format(cascadeAttribute, belongs.Cascade) );
			//			String sort = (hasAndBelongsTo.Sort == null ? "" : String.Format(sortAttribute, hasAndBelongsTo.Sort));
			String orderBy = ( belongs.OrderBy == null ? "" : String.Format(orderByAttribute, belongs.OrderBy) );
			String where = ( belongs.Where == null ? "" : String.Format(whereAttribute, belongs.Where) );

			builder.AppendFormat(idbagOpen, name, table + schema + lazy + inverse + cascade + orderBy + where);
			
			string colColumn = GetXmlString(columnAttribute, ConvertString(collection.Column));
			string colType = GetXmlString(typeAttribute, ConvertString(collection.ColumnType));

			builder.AppendFormat(collectionIdOpen, colColumn + colType);
			if (collection.Generator != CollectionIDType.None)
			{
				builder.AppendFormat(generatorOpen, collection.Generator.ToString().ToLower());
				
				//TODO: map other collectionID types.
				if (collection.Generator == CollectionIDType.HiLo)
				{
					HiloAttribute hilo = GetHiloAttribute(prop.GetCustomAttributes(false));
					builder.AppendFormat(paramElement, "table", hilo.Table);
					builder.AppendFormat(paramElement, "column", hilo.Column);
					builder.AppendFormat(paramElement, "max_lo", hilo.MaxLo);
				}
				builder.Append(generatorClose);
			}
			builder.Append(collectionIdClose);

			Type otherType = belongs.MapType;
			String columnkey = ConvertString(belongs.ColumnKey);
			String column = GetXmlString(columnAttribute, ConvertString(belongs.Column));

			builder.AppendFormat(keyTag, columnkey);

			// We need to choose from element, one-to-many, many-to-many, composite-element, many-to-any
			// We need to do it wisely
			if (column != null)
			{
				builder.AppendFormat(manyToMany, otherType.AssemblyQualifiedName, column);
			}

			builder.Append(idbagClose);
		}

		public static string ConvertString(object checkString)
		{
			if (checkString == null) return String.Empty;
			if (checkString is string && ((string)checkString).Trim().Length > 0) return checkString as string;
			if (checkString is bool)	
				if ((bool)checkString)
					return ((bool)checkString).ToString().ToLower(); 
			
			return String.Empty;

		} 
		public static string GetXmlString(string format, string stuff)
		{
			return (stuff == String.Empty) ? stuff : String.Format(format, stuff);	
		}

		private HiloAttribute GetHiloAttribute(object[] attributes)
		{
			HiloAttribute attrib = null;
			foreach (object a in attributes)
				if (a is HiloAttribute && (attrib = (HiloAttribute) a) != null) break;
			return (attrib == null) ? new HiloAttribute() : attrib;
		}
	}
}
