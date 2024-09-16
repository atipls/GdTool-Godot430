using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GdTool;

public enum VariantType : int {
	NIL,

	// atomic types
	BOOL,
	INT,
	FLOAT,
	STRING,

	// math types
	VECTOR2,
	VECTOR2I,
	RECT2,
	RECT2I,
	VECTOR3,
	VECTOR3I,
	TRANSFORM2D,
	VECTOR4,
	VECTOR4I,
	PLANE,
	QUATERNION,
	AABB,
	BASIS,
	TRANSFORM3D,
	PROJECTION,

	// misc types
	COLOR,
	STRING_NAME,
	NODE_PATH,
	RID,
	OBJECT,
	CALLABLE,
	SIGNAL,
	DICTIONARY,
	ARRAY,

	// typed arrays
	PACKED_BYTE_ARRAY,
	PACKED_INT32_ARRAY,
	PACKED_INT64_ARRAY,
	PACKED_FLOAT32_ARRAY,
	PACKED_FLOAT64_ARRAY,
	PACKED_STRING_ARRAY,
	PACKED_VECTOR2_ARRAY,
	PACKED_VECTOR3_ARRAY,
	PACKED_COLOR_ARRAY,
	PACKED_VECTOR4_ARRAY,

	VARIANT_MAX
};

class VariantObject(string type, Dictionary<string, Variant> properties) {
	public string Type => type;
	public Dictionary<string, Variant> Properties => properties;
}

class Variant(VariantType type, object value) {
	public VariantType Type => type;
	public dynamic Value => value;

	public static Variant OfNil => new(VariantType.NIL, null);
	public static Variant OfBool(bool value) => new(VariantType.BOOL, value);
	public static Variant OfInt(int value) => new(VariantType.INT, value);
	public static Variant OfInt64(long value) => new(VariantType.INT, value);
	public static Variant OfFloat(float value) => new(VariantType.FLOAT, value);
	public static Variant OfFloat64(double value) => new(VariantType.FLOAT, value);
	public static Variant OfString(string value) => new(VariantType.STRING, value);
	public static Variant OfVector2(Vector2 value) => new(VariantType.VECTOR2, value);
	public static Variant OfVector2i(Vector2 value) => new(VariantType.VECTOR2I, value);
	public static Variant OfRect2(dynamic value) => new(VariantType.RECT2, value);
	public static Variant OfRect2i(dynamic value) => new(VariantType.RECT2I, value);
	public static Variant OfVector3(Vector3 value) => new(VariantType.VECTOR3, value);
	public static Variant OfVector3i(Vector3 value) => new(VariantType.VECTOR3I, value);
	// public static Variant OfTransform2D(Transform2D value) => new(VariantType.TRANSFORM2D, value);
	public static Variant OfVector4(Vector4 value) => new(VariantType.VECTOR4, value);
	public static Variant OfVector4i(Vector4 value) => new(VariantType.VECTOR4I, value);
	public static Variant OfPlane(Plane value) => new(VariantType.PLANE, value);
	public static Variant OfQuaternion(Quaternion value) => new(VariantType.QUATERNION, value);
	// public static Variant OfAABB(AABB value) => new(VariantType.AABB, value);
	public static Variant OfBasis(Basis value) => new(VariantType.BASIS, value);
	// public static Variant OfTransform3D(Transform3D value) => new(VariantType.TRANSFORM3D, value);
	// public static Variant OfProjection(Projection value) => new(VariantType.PROJECTION, value);
	public static Variant OfColor(Color value) => new(VariantType.COLOR, value);
	public static Variant OfStringName(string value) => new(VariantType.STRING_NAME, value);
	public static Variant OfNodePath(string value) => new(VariantType.NODE_PATH, value);
	public static Variant OfRID(ulong value) => new(VariantType.RID, value);
	public static Variant OfObject(object value) => new(VariantType.OBJECT, value);
	public static Variant OfCallable(object value) => new(VariantType.CALLABLE, value);
	public static Variant OfSignal(object value) => new(VariantType.SIGNAL, value);
	public static Variant OfDictionary(Dictionary<Variant, Variant> value) => new(VariantType.DICTIONARY, value);
	public static Variant OfArray(Variant[] value) => new(VariantType.ARRAY, value);
	public static Variant OfPackedByteArray(byte[] value) => new(VariantType.PACKED_BYTE_ARRAY, value);
	public static Variant OfPackedInt32Array(int[] value) => new(VariantType.PACKED_INT32_ARRAY, value);
	public static Variant OfPackedInt64Array(long[] value) => new(VariantType.PACKED_INT64_ARRAY, value);
	public static Variant OfPackedFloat32Array(float[] value) => new(VariantType.PACKED_FLOAT32_ARRAY, value);
	public static Variant OfPackedFloat64Array(double[] value) => new(VariantType.PACKED_FLOAT64_ARRAY, value);
	public static Variant OfPackedStringArray(string[] value) => new(VariantType.PACKED_STRING_ARRAY, value);
	public static Variant OfPackedVector2Array(Vector2[] value) => new(VariantType.PACKED_VECTOR2_ARRAY, value);
	public static Variant OfPackedVector3Array(Vector3[] value) => new(VariantType.PACKED_VECTOR3_ARRAY, value);
	public static Variant OfPackedColorArray(Color[] value) => new(VariantType.PACKED_COLOR_ARRAY, value);
	public static Variant OfPackedVector4Array(Vector4[] value) => new(VariantType.PACKED_VECTOR4_ARRAY, value);
	
	public override string ToString() {
		switch (Type) {
			case VariantType.NIL: return "nil";
			case VariantType.BOOL: return (bool)Value ? "true" : "false";
			case VariantType.INT: return value.ToString();
			case VariantType.FLOAT: return value.ToString();
			case VariantType.STRING: return $"\"{value}\"";
			case VariantType.VECTOR2: return $"Vector2({Value.X}, {Value.Y})";
			case VariantType.VECTOR2I: return $"Vector2i({(int)Value.X}, {(int)Value.Y})";
			// case VariantType.RECT2: return value.ToString();
			// case VariantType.RECT2I: return value.ToString();
			case VariantType.VECTOR3: return $"Vector3({Value.X}, {Value.Y}, {Value.Z})";
			case VariantType.VECTOR3I: return $"Vector3i({(int)Value.X}, {(int)Value.Y}, {(int)Value.Z})";
			case VariantType.VECTOR4: return $"Vector4({Value.X}, {Value.Y}, {Value.Z}, {Value.W})";
			case VariantType.VECTOR4I: return $"Vector4i({(int)Value.X}, {(int)Value.Y}, {(int)Value.Z}, {(int)Value.W})";
			// case VariantType.PLANE: return value.ToString();
			// case VariantType.QUATERNION: return value.ToString();
			// case VariantType.BASIS: return value.ToString();
			case VariantType.COLOR: return $"Color({Value.R}, {Value.G}, {Value.B}, {Value.A})";
			case VariantType.STRING_NAME: return $"\"{value}\"";
			case VariantType.NODE_PATH: return $"\"{value}\"";
			case VariantType.RID: return value.ToString();
			case VariantType.OBJECT: return $"Object({Value.Type}, {string.Join(", ", ((Dictionary<string, Variant>)Value.Properties).Select(kv => $"\"{kv.Key}\":{kv.Value}"))})"; 
			// case VariantType.CALLABLE: return value.ToString();
			// case VariantType.SIGNAL: return value.ToString();
			case VariantType.DICTIONARY: return "{\n    " + string.Join(",\n    ", ((Dictionary<Variant, Variant>)Value).Select(kv => $"{kv.Key}: {kv.Value}")) + "\n}";
			case VariantType.ARRAY: return "[\n    " + string.Join(",\n    ", (object[])Value) + "\n]";
			case VariantType.PACKED_BYTE_ARRAY: return $"PackedByteArray({string.Join(", ", Value)})";
			case VariantType.PACKED_INT32_ARRAY: return $"PackedInt32Array({string.Join(", ", Value)})";
			case VariantType.PACKED_INT64_ARRAY: return $"PackedInt64Array({string.Join(", ", Value)})";
			case VariantType.PACKED_FLOAT32_ARRAY: return $"PackedFloat32Array({string.Join(", ", Value)})";
			case VariantType.PACKED_FLOAT64_ARRAY: return $"PackedFloat64Array({string.Join(", ", Value)})";
			case VariantType.PACKED_STRING_ARRAY: return $"PackedStringArray({string.Join(", ", ((string[])Value).Select(s => $"\"{s}\""))})";
			case VariantType.PACKED_VECTOR2_ARRAY: return $"PackedVector2Array({string.Join(", ", Value)})";
			case VariantType.PACKED_VECTOR3_ARRAY: return $"PackedVector3Array({string.Join(", ", Value)})";
			case VariantType.PACKED_COLOR_ARRAY: return $"PackedColorArray({string.Join(", ", Value)})";
			case VariantType.PACKED_VECTOR4_ARRAY: return $"PackedVector4Array({string.Join(", ", Value)})";
			default: return "unknown";
		}
	}
}