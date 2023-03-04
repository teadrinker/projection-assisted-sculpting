# TeaImg 

This wraps calculation of arrays of float4 (Color/Vector4 etc) into a neat [fluent-interface-style](https://en.wikipedia.org/wiki/Fluent_interface) API with a dual CPU/GPU backend.
Actual calculation is not performed until you call RequestPixel(s) or RequestTexture.

    var ctx = new 
    var yellow = ctx.DataSource(Color.red).add(ctx.DataSource(Color.green)).RequestPixel(); 
    var yellowArray = ctx.DataSource(new Color[Color.red]).add(ctx.DataSource(new Color[Color.green])).RequestPixels();


### Build-in functions

If you pass a single value instead of vector to these functions, a specialized constant case will be used internally.

```
neg() 
abs()
sin() 
cos() 
floor() 
frac()
square() 
cube() 

absdiff(b)
add(b)
sub(b)
mul(b)
div(b)
mod(b)
lessThan(b)
lessThanOrEqual(b)
greaterThan(b)
greaterThanOrEqual(b) 
and(b) 
or(b)
min(b)
max(b)
pow(b)

lerp(b, t) 
clamp(mi, ma)
inRange(mi, ma)

reduceSum()
reduceMean()
reduceProd()
reduceMin()
reduceMax()

blur(int radius, float type = 2f)
   
   // blur type (interpolates between these types)
   // 0 : rect window
   // 1 : triangular window
   // 2 : quadratic window
   // 3 : Hann window (sine curve)

grayScale(mulR, mulG, mulB, offset)
channelMix(Matrix4x4 mat, Vector4 offset) 

convolve(b) {
convolve1DHorizontal(b)
convolve1DVertical(b)

```

### Extend API

Add a custom GPU command:

    ctx.Add("inpaint", 1, Resources.Load<Shader>("inpaint"));

Use it like this:

    img.call("inpaint")

Complete processing:

    var inpainted_image = ctx.DataSource(image).call("inpaint").RequestPixels();

