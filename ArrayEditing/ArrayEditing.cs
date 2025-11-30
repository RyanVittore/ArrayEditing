using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Elements.Core;

using FrooxEngine;
using FrooxEngine.UIX;

using HarmonyLib;

using ResoniteModLoader;

namespace ArrayEditing;

public class ArrayEditing : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.2";
	public override string Name => "Array Editing";
	public override string Author => "Ryan Vittore";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/RyanVittore/ArrayEditing";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> Enabled = new("Enabled", "Should Arrays generate a custom editor. Applies to new arrays.", () => true);

	private static ModConfiguration Config;

	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config!.Save(true);

		Harmony harmony = new("RyanVittore.ArrayEditing");
		harmony.PatchAll();
	}
	[HarmonyPatch(typeof(SyncMemberEditorBuilder), "BuildArray")]
	internal sealed class ArrayEditor {
		private static readonly MethodInfo _addCurveValueProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddCurveValueProxying));
		private static readonly MethodInfo _addLinearValueProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddLinearValueProxying));
		private static readonly MethodInfo _addListReferenceProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddListReferenceProxying));
		private static readonly MethodInfo _addListValueProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddListValueProxying));
		private static readonly Type _iWorldElementType = typeof(IWorldElement);

		private static readonly MethodInfo _setLinearPoint = AccessTools.Method(typeof(ArrayEditor), nameof(SetLinearPoint));
		private static readonly MethodInfo _setCurvePoint = AccessTools.Method(typeof(ArrayEditor), nameof(SetCurvePoint));

		private static bool _skipListChanges = false;

		private static void AddCurveValueProxying<T>(SyncArray<CurveKey<T>> array, SyncElementList<ValueGradientDriver<T>.Point> list)
			where T : IEquatable<T> {
			foreach (var key in array) {
				var point = list.Add();
				point.Position.Value = key.time;
				point.Value.Value = key.value;
			}

			AddUpdateProxies(array, list, list.Elements);

			list.ElementsAdded += (list, startIndex, count) => {
				var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
				var buffer = addedElements.Select(point => new CurveKey<T>(point.Position, point.Value)).ToArray();

				if (!_skipListChanges) {
					array.Changed -= ArrayChanged;
					array.Insert(buffer, startIndex);
					array.Changed += ArrayChanged;
				}

				AddUpdateProxies(array, list, addedElements);
			};

			list.ElementsRemoved += (list, startIndex, count) => {
				if (_skipListChanges) return;
				if (array.Count < startIndex + count) return;
				array.Changed -= ArrayChanged;
				array.Remove(startIndex, count);
				array.Changed += ArrayChanged;
			};
		}

		private static void AddLinearValueProxying<T>(SyncArray<LinearKey<T>> array, SyncElementList<ValueGradientDriver<T>.Point> list)
			where T : IEquatable<T> {
			foreach (var key in array) {
				var point = list.Add();
				point.Position.Value = key.time;
				point.Value.Value = key.value;
			}

			AddUpdateProxies(array, list, list.Elements);

			list.ElementsAdded += (list, startIndex, count) => {
				var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
				var buffer = addedElements.Select(point => new LinearKey<T>(point.Position, point.Value)).ToArray();

				if (!_skipListChanges) {
					array.Changed -= ArrayChanged;
					array.Insert(buffer, startIndex);
					array.Changed += ArrayChanged;
				}
				AddUpdateProxies(array, list, addedElements);
			};

			list.ElementsRemoved += (list, startIndex, count) => {
				if (_skipListChanges) return;
				if (array.Count < startIndex + count) return;
				array.Changed -= ArrayChanged;
				array.Remove(startIndex, count);
				array.Changed += ArrayChanged;
			};
		}

		private static void AddListReferenceProxying<T>(SyncArray<T> array, SyncElementList<SyncRef<T>> list)
			where T : class, IEquatable<T>, IWorldElement {
			foreach (var reference in array) {
				var syncRef = list.Add();
				syncRef.Target = reference;
			}

			AddUpdateProxies(array, list, list.Elements);

			list.ElementsAdded += (list, startIndex, count) => {
				var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
				var buffer = addedElements.Select(syncRef => syncRef.Target).ToArray();

				if (!_skipListChanges) {
					array.Changed -= ArrayChanged;
					array.Insert(buffer, startIndex);
					array.Changed += ArrayChanged;
				}
				AddUpdateProxies(array, list, addedElements);
			};

			list.ElementsRemoved += (list, startIndex, count) => {
				if (_skipListChanges) return;
				if (array.Count < startIndex + count) return;
				array.Changed -= ArrayChanged;
				array.Remove(startIndex, count);
				array.Changed += ArrayChanged;
			};
		}

		private static void AddListValueProxying<T>(SyncArray<T> array, SyncElementList<Sync<T>> list)
			where T : IEquatable<T> {
			foreach (var value in array) {
				var sync = list.Add();
				sync.Value = value;
			}

			AddUpdateProxies(array, list, list.Elements);

			list.ElementsAdded += (list, startIndex, count) => {
				var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
				var buffer = addedElements.Select(sync => sync.Value).ToArray();

				if (!_skipListChanges) {
					array.Changed -= ArrayChanged;
					array.Insert(buffer, startIndex);
					array.Changed += ArrayChanged;
				}
				AddUpdateProxies(array, list, addedElements);
			};

			list.ElementsRemoved += (list, startIndex, count) => {
				if (_skipListChanges) return;
				if (array.Count < startIndex + count) return;
				array.Changed -= ArrayChanged;
				array.Remove(startIndex, count);
				array.Changed += ArrayChanged;
			};
		}

		private static void AddTubePointProxying(SyncArray<TubePoint> array, SyncElementList<ValueGradientDriver<float3>.Point> list) {
			foreach (var tubePoint in array) {
				var point = list.Add();
				point.Position.Value = tubePoint.radius;
				point.Value.Value = tubePoint.position;
			}

			AddUpdateProxies(array, list, list.Elements);

			list.ElementsAdded += (list, startIndex, count) => {
				var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
				var buffer = addedElements.Select(point => new TubePoint(point.Value.Value, point.Position.Value)).ToArray();

				if (!_skipListChanges) {
					array.Changed -= ArrayChanged;
					array.Insert(buffer, startIndex);
					array.Changed += ArrayChanged;
				}
				AddUpdateProxies(array, list, addedElements);
			};

			list.ElementsRemoved += (list, startIndex, count) => {
				if (_skipListChanges) return;
				if (array.Count < startIndex + count) return;
				array.Changed -= ArrayChanged;
				array.Remove(startIndex, count);
				array.Changed += ArrayChanged;
			};
		}

		private static void AddUpdateProxies<T>(SyncArray<LinearKey<T>> array,
			SyncElementList<ValueGradientDriver<T>.Point> list, IEnumerable<ValueGradientDriver<T>.Point> elements)
					where T : IEquatable<T> {
			foreach (var point in elements) {
				point.Changed += syncObject => {
					if (_skipListChanges) return;
					var index = list.IndexOfElement(point);
					array.Changed -= ArrayChanged;
					array[index] = new LinearKey<T>(point.Position, point.Value);
					array.Changed += ArrayChanged;
				};
			}
		}

		private static void AddUpdateProxies<T>(SyncArray<T> array, SyncElementList<Sync<T>> list, IEnumerable<Sync<T>> elements)
					where T : IEquatable<T> {
			foreach (var sync in elements) {
				sync.OnValueChange += field => {
					if (_skipListChanges) return;
					var index = list.IndexOfElement(sync);
					array.Changed -= ArrayChanged;
					array[index] = sync.Value;
					array.Changed += ArrayChanged;
				};
			}
		}

		private static void AddUpdateProxies<T>(SyncArray<T> array, SyncElementList<SyncRef<T>> list, IEnumerable<SyncRef<T>> elements)
			where T : class, IEquatable<T>, IWorldElement {
			foreach (var sync in elements) {
				sync.OnValueChange += field => {
					if (_skipListChanges) return;
					var index = list.IndexOfElement(sync);
					array.Changed -= ArrayChanged;
					array[index] = sync.Target;
					array.Changed += ArrayChanged;
				};
			}
		}

		private static void AddUpdateProxies(SyncArray<TubePoint> array, SyncElementList<ValueGradientDriver<float3>.Point> list, IEnumerable<ValueGradientDriver<float3>.Point> elements) {
			foreach (var point in elements) {
				point.Changed += field => {
					if (_skipListChanges) return;
					var index = list.IndexOfElement(point);
					var tubePoint = new TubePoint(point.Value.Value, point.Position.Value);
					array.Changed -= ArrayChanged;
					array[index] = tubePoint;
					array.Changed += ArrayChanged;
				};
			}
		}

		private static void AddUpdateProxies<T>(SyncArray<CurveKey<T>> array,
			SyncElementList<ValueGradientDriver<T>.Point> list, IEnumerable<ValueGradientDriver<T>.Point> elements)
					where T : IEquatable<T> {
			foreach (var point in elements) {
				point.Changed += syncObject => {
					if (_skipListChanges) return;
					var index = list.IndexOfElement(point);
					array.Changed -= ArrayChanged;
					array[index] = new CurveKey<T>(point.Position, point.Value, array[index].leftTangent, array[index].rightTangent);
					array.Changed += ArrayChanged;
				};
			}
		}

		private static bool Prefix(ISyncArray array, string name, FieldInfo fieldInfo, UIBuilder ui, float labelSize) {
			if (!Config.GetValue(Enabled)) {
				return true; //Run original when disabled
			}
			if (!TryGetGenericParameter(typeof(SyncArray<>), array.GetType(), out var arrayType))
				return false;

			ui.Panel().Slot.GetComponent<LayoutElement>();
			Slot slot = SyncMemberEditorBuilder.GenerateMemberField(array, name, ui, 0.3f);
			ui.ForceNext = slot.AttachComponent<RectTransform>();
			ui.Text("(Proxy Array)");
			ui.NestOut();

			var isSyncLinear = TryGetGenericParameter(typeof(SyncLinear<>), array.GetType(), out var syncLinearType);
			var isSyncCurve = TryGetGenericParameter(typeof(SyncCurve<>), array.GetType(), out var syncCurveType);

			var proxySlotName = $"{name}-{array.ReferenceID}-Proxy";
			var proxiesSlot = ui.World.AssetsSlot;
			var newProxy = false;
			if (proxiesSlot.FindChild(proxySlotName) is not Slot proxySlot) {
				proxySlot = proxiesSlot.AddSlot(proxySlotName);
				array.FindNearestParent<IDestroyable>().Destroyed += (IDestroyable _) => proxySlot.Destroy();
				newProxy = true;
			}
			proxySlot.PersistentSelf = false;
			proxySlot.GetComponentOrAttach<DestroyOnUserLeave>(d => d.TargetUser.Target == slot.LocalUser).TargetUser.Target = slot.LocalUser;

			ISyncList list;
			FieldInfo listField;

			if (isSyncLinear && SupportsLerp(syncLinearType!)) {
				var gradientType = typeof(ValueGradientDriver<>).MakeGenericType(syncLinearType);
				var gradient = GetOrAttachComponent(proxySlot, gradientType, out var attachedNew);

				list = (ISyncList)gradient.GetSyncMember(nameof(ValueGradientDriver<float>.Points));
				listField = gradient.GetSyncMemberFieldInfo(nameof(ValueGradientDriver<float>.Points));

				if (attachedNew) {
					_addLinearValueProxying.MakeGenericMethod(syncLinearType).Invoke(null, [array, list]);
				}
			} else if (isSyncCurve && SupportsLerp(syncCurveType!)) {
				var gradientType = typeof(ValueGradientDriver<>).MakeGenericType(syncCurveType);
				var gradient = GetOrAttachComponent(proxySlot, gradientType, out var attachedNew);

				list = (ISyncList)gradient.GetSyncMember(nameof(ValueGradientDriver<float>.Points));
				listField = gradient.GetSyncMemberFieldInfo(nameof(ValueGradientDriver<float>.Points));

				if (attachedNew) {
					_addCurveValueProxying.MakeGenericMethod(syncCurveType).Invoke(null, [array, list]);
				}
			} else {
				if (arrayType == typeof(TubePoint)) {
					var gradient = GetOrAttachComponent(proxySlot, typeof(ValueGradientDriver<float3>), out var attachedNew);

					list = (ISyncList)gradient.GetSyncMember(nameof(ValueGradientDriver<float3>.Points));
					listField = gradient.GetSyncMemberFieldInfo(nameof(ValueGradientDriver<float3>.Points));

					if (attachedNew) {
						AddTubePointProxying((SyncArray<TubePoint>)array, (SyncElementList<ValueGradientDriver<float3>.Point>)list);
					}
				} else if (Coder.IsEnginePrimitive(arrayType)) {
					var multiplexerType = typeof(ValueMultiplexer<>).MakeGenericType(arrayType);
					var multiplexer = GetOrAttachComponent(proxySlot, multiplexerType, out var attachedNew);
					list = (ISyncList)multiplexer.GetSyncMember(nameof(ValueMultiplexer<float>.Values));
					listField = multiplexer.GetSyncMemberFieldInfo(nameof(ValueMultiplexer<float>.Values));

					if (attachedNew)
						_addListValueProxying.MakeGenericMethod(arrayType).Invoke(null, [array, list]);
				} else if (_iWorldElementType.IsAssignableFrom(arrayType)) {
					var multiplexerType = typeof(ReferenceMultiplexer<>).MakeGenericType(arrayType);
					var multiplexer = GetOrAttachComponent(proxySlot, multiplexerType, out var attachedNew);
					list = (ISyncList)multiplexer.GetSyncMember(nameof(ReferenceMultiplexer<Slot>.References));
					listField = multiplexer.GetSyncMemberFieldInfo(nameof(ReferenceMultiplexer<Slot>.References));

					if (attachedNew)
						_addListReferenceProxying.MakeGenericMethod(arrayType).Invoke(null, [array, list]);
				} else {
					proxySlot.Destroy();
					return false;
				}
			}

			if (!array.IsDriven) {
				SyncMemberEditorBuilder.BuildList(list, name, listField, ui);
				var listSlot = ui.Current;
				listSlot.GetComponentOrAttach<DestroyOnUserLeave>(d => d.TargetUser.Target == slot.LocalUser).TargetUser.Target = slot.LocalUser;
				listSlot.PersistentSelf = false;
				void ArrayDriveCheck(IChangeable changeable) {
					if (((ISyncArray)changeable).IsDriven) {
						listSlot.DestroyChildren();
						listSlot.Components.ToArray().Do((Component c) => c.Destroy());
						listSlot.AttachComponent<LayoutElement>().MinHeight.Value = 24f;
						var newUi = new UIBuilder(listSlot, listSlot);
						RadiantUI_Constants.SetupEditorStyle(newUi);
						newUi.Text("(array is driven)");
						proxySlot?.Destroy();
						array.Changed -= ArrayDriveCheck;
					}
				}
				array.Changed += ArrayDriveCheck;
			} else {
				LocaleString text = "(array is driven)";
				ui.Text(in text);
			}

			if (newProxy) {
				array.Changed += ArrayChanged;
			}

			return false;
		}

		static void SetLinearPoint<T>(ValueGradientDriver<T>.Point point, LinearKey<T> arrayElem) where T : IEquatable<T> {
			point.Position.Value = arrayElem.time;
			point.Value.Value = arrayElem.value;
		}

		static void SetCurvePoint<T>(ValueGradientDriver<T>.Point point, CurveKey<T> arrayElem) where T : IEquatable<T> {
			point.Position.Value = arrayElem.time;
			point.Value.Value = arrayElem.value;
		}

		static void SetTubePoint(ValueGradientDriver<float3>.Point point, TubePoint arrayElem) {
			point.Position.Value = arrayElem.radius;
			point.Value.Value = arrayElem.position;
		}

		static void ArrayChanged(IChangeable changeable) {
			Debug("Array Changed");
			var array = (ISyncArray)changeable;

			if (array.IsDriven) {
				array.Changed -= ArrayChanged;
				return;
			}

			var proxySlotName = $"{array.Name}-{array.ReferenceID}-Proxy";
			var proxiesSlot = array.World.AssetsSlot;
			if (proxiesSlot.FindChild(proxySlotName) is Slot proxySlot) {
				ISyncList? list = null;
				foreach (var comp in proxySlot.Components) {
					if (comp.GetType().IsGenericType && comp.GetType().GetGenericTypeDefinition() == typeof(ValueMultiplexer<>)) {
						list = comp.GetSyncMember("Values") as ISyncList;
						_skipListChanges = true;
						list.World.RunSynchronously(() => _skipListChanges = false);
						list.EnsureExactElementCount(array.Count);
						for (int i = 0; i < array.Count; i++) {
							((IField)list.GetElement(i)).BoxedValue = array.GetElement(i);
						}
					} else if (comp.GetType().IsGenericType && comp.GetType().GetGenericTypeDefinition() == typeof(ReferenceMultiplexer<>)) {
						list = comp.GetSyncMember("References") as ISyncList;
						_skipListChanges = true;
						list.World.RunSynchronously(() => _skipListChanges = false);
						list.EnsureExactElementCount(array.Count);
						for (int i = 0; i < array.Count; i++) {
							((ISyncRef)list.GetElement(i)).Target = (IWorldElement)array.GetElement(i);
						}
					} else if (comp.GetType().IsGenericType && comp.GetType().GetGenericTypeDefinition() == typeof(ValueGradientDriver<>)) {
						list = comp.GetSyncMember("Points") as ISyncList;
						_skipListChanges = true;
						list.World.RunSynchronously(() => _skipListChanges = false);
						list.EnsureExactElementCount(array.Count);

						var isSyncLinear = TryGetGenericParameter(typeof(SyncLinear<>), array.GetType(), out var syncLinearType);
						var isSyncCurve = TryGetGenericParameter(typeof(SyncCurve<>), array.GetType(), out var syncCurveType);

						if (!TryGetGenericParameter(typeof(SyncArray<>), array.GetType(), out var genericParameter))
							return;

						var arrayType = genericParameter;

						for (int i = 0; i < array.Count; i++) {
							var elem = list.GetElement(i);

							if (isSyncLinear && SupportsLerp(syncLinearType!)) {
								_setLinearPoint.MakeGenericMethod(syncLinearType).Invoke(null, [elem, array.GetElement(i)]);
							} else if (isSyncCurve && SupportsLerp(syncCurveType!)) {
								_setCurvePoint.MakeGenericMethod(syncCurveType).Invoke(null, [elem, array.GetElement(i)]);
							} else {
								if (arrayType == typeof(TubePoint)) {
									SetTubePoint((ValueGradientDriver<float3>.Point)elem!, (TubePoint)array.GetElement(i));
								}
							}
						}
					}
				}
			}
		}

		private static Component GetOrAttachComponent(Slot targetSlot, Type type, out bool attachedNew) {
			attachedNew = false;

			if (targetSlot.GetComponent(type) is not Component comp) {
				comp = targetSlot.AttachComponent(type);
				attachedNew = true;
			}

			return comp;
		}

		private static bool SupportsLerp(Type type) {
			var coderType = typeof(Coder<>).MakeGenericType(type);
			return Traverse.Create(coderType).Property<bool>(nameof(Coder<float>.SupportsLerp)).Value;
		}

		private static bool TryGetGenericParameter(Type baseType, Type concreteType, out Type? genericParameter) {
			genericParameter = null;

			if (concreteType is null || baseType is null || !baseType.IsGenericType)
				return false;

			if (concreteType.IsGenericType && concreteType.GetGenericTypeDefinition() == baseType) {
				var genericArguments = concreteType.GetGenericArguments();
				if (genericArguments.Length > 0) {
					genericParameter = genericArguments[0];
					return true;
				}
			}
			return TryGetGenericParameter(baseType, concreteType.BaseType, out genericParameter);
		}
	}

	[HarmonyPatch(typeof(ListEditor), "BuildListElement")]
	internal class ListEditor_BuildListElement_Patch {
		public static void Prefix(UIBuilder ui) {
			if (Config.GetValue(Enabled)) {
				ui.Style.MinHeight = 24f;
			}
		}
	}

	[HarmonyPatch(typeof(SyncMemberEditorBuilder), "GenerateMemberField")]
	internal class SyncMemberEditorBuilder_GenerateMemberField_Patch {
		public static void Prefix(ISyncMember member, UIBuilder ui) {
			if (!Config.GetValue(Enabled) || member.Parent is not ISyncList || member is not SyncObject)
				return;

			ui.CurrentRect.Slot.AttachComponent<HorizontalLayout>();
			if (ui.CurrentRect.Slot.GetComponent<LayoutElement>() is LayoutElement layoutElement) {
				layoutElement.MinWidth.Value = 48f;
				layoutElement.FlexibleWidth.Value = -1f;
			}
		}
	}
}
