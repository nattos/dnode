using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DNode {
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public class VectorAttribute : Attribute {
    public int Dims { get; private set; }

    public VectorAttribute(int dims) {
      Dims = dims;
    }
  }
   
  public class IntegerAttribute : VectorAttribute {
    public IntegerAttribute() : base(1) {}
  }
 
  public class BooleanAttribute : VectorAttribute {
    public BooleanAttribute() : base(1) {}
  }
  
  public class ScalarAttribute : VectorAttribute {
    public ScalarAttribute() : base(1) {}
  }

  public class Vector2Attribute : VectorAttribute {
    public Vector2Attribute() : base(2) {}
  }

  public class Vector3Attribute : VectorAttribute {
    public Vector3Attribute() : base(3) {}
  }

  public class Vector4Attribute : VectorAttribute {
    public Vector4Attribute() : base(4) {}
  }
}
