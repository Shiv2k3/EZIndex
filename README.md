<h1>Welcome to EZ Indexing!</h1>

EZIndex is used for spatial indexing, its written in C# and the library can be purchased on the Unity Asset Store. This is a quick tutorial on the library. EZIndex offers 3 domain types - Grid, Lattice, and Sphere. 

<h2>2D Grids</h2>

Grid is the 2 dimensional indexing space, and the name of the static struct that contians it's corresponding methods.
This is how you would iterate nodes in a 9x6 grid centered at the origin using Grid -
```C#
using EZ.Index;

var ratio = new float2(9, 6);
var total = Grid.GetTotal(ratio, Domain.Centers);
for (int n = 0; n < total; n++)
{
  var node = Grid.CenterNode(in n, in ratio);
}
```
![9x6 grid of nodes](/Images/9x6grid.png)

<h3>The Domain enum</h3>
Domain enum describes what offsets the nodes are in. In the example, `Domain.Centers` means the nodes will be in the center of each unit within the domain's bounds. Whereas `Domain.Corners` means the nodes will be in the corners of each unit in the domains bounds. The last type `Domain.Whole` means the nodes will always have integer coordinates located in the first quadrant/octant, so only whole numbered nodes!

<h2>3D Lattices</h2>

To achive something similar in 3D, you would simply change the 2 calls to Grid with Lattice, and use float3 for the ratio:
```C#
var ratio = new float3(9, 6, 6);
var total = Lattice.GetTotal(ratio, Domain.Centers);
for (int n = 0; n < total; n++)
{
  var node = Lattice.CenterNode(in n, in ratio);
}
```
![9x6x6 node lattice](Images/9x6x6lattice.png)

<h2>Spherical Domain</h2>

The Spherical domain is useful for iterating over a spherical surface. The syntax for iterating ndoes is almost the same, except the usage of a custom struct Angle over the float2 to store the angles, and a node-layers system.

```C#
var layers = 10;
var total = Spherical.GetTotal(layers);
for (int n = 0; n < total; n++)
{
  var node = Spherical.GetNode(in n, in layers); // typeof(node) = Angle
}
```
![sphere with 13 node-layers](Images/13layers.png) <br/>

<h3>How is the total number of nodes on the sphere surface calculated?</h3>

It sound complex, but layers simply means the number of vertical layers/rings of nodes on the sphere surface. The math behind it is complex and understanding it isn't required for usage, but for a 10 layer sphere surface, there are exactly 102 nodes, 1 north pole node (index 0), 1 south pole node (index 101), and 50 in each hemisphere (index [1, 100]). The count of nodes in each layer moving towards the equator has 4 more than the previous layer, not including the poles. <br/>

Here is a visual example of this method in shader toy: https://www.shadertoy.com/view/NtKyWV. <br/>

![10 layer based sphere](Images/10layers.png)

<h2>Node Hashing</h2>

This library wouldn't be complete if it didn't include a way to get the index when given a node. 
A possible use case for node hashing could be player picking up consumeable healthdrops.
```C#
var healthdrops = new Dictionary<int, GameObject>(); // Image this stores the mapping of node indices to healthdrop GOs
var node = Grid.SnapCenter(playerPosition.xy, ratio.xy); // Snap the player's position to the nearest node on the centered grid
var index = Grid.CenterIndex(node, ratio.xy); // calculate the new node's index
if (healthdrops.TryGetValue(index, out var drop)) // get the drop if it exists
{
    var closeEnough = distance(playerPosition, drop.transform.position) <= 1;
    if (closeEnough) // check if its close enough to be picked up
    {
        healthdrops.Remove(index); // consume
        Destroy(drop); // consume
    }
}
```
![9x6 indexed grid](Images/indexedgrid.png)
![hippo](Images/hearts.gif)
