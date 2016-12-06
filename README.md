# AmbientResource
Quite a few times in my career I have encountered code that requires access to a resource, e.g. an SqlConnection, where the resource instance is created in the entry method and then passed along to all other methods called from within that method. This is of course much better than firing up a new instance of the resource in every method, however, it really pollutes the method signatures with parameters that, in essence, are irrelevant to the purpose of the method.

There are many ways to solve this issue, (dependency) injection and ambience are examples of such, however I quite like the ambient way of accessing a resource, and thus I decided to roll my own utility to simplify writing this type of code. The main concern with the ambient approach is, in my opinion, that it hides the specifics of how and when the resource is created/disposed.

The scenarios I most often encounter are request-scoped where one instance of the resource is required (i.e. instantiated) for each request. All subprocessing going on within that one request will all utilize the same resource. This is the basis for my **VERY** simple implementation of an ambient resource.

The concept is fairly simple: A thread-safe (singleton) property, AmbientResource.Current ,represents the ambient resource and encapsulates instantiation of the “actual resource” by requiring the caller/user to specify a factory which will return an instance of the “actual resource”. This factory is used internally by the ambient resource to create a single instance of the “actual resource” which is used internally until the ambient resource is disposed.

Consider the example below which is a perfect example of a signature being polluted with a parameter which in essence is irrelevant:
```cs
void Method1()
{
    using (var conn = new SqlConnection(/* connection string omitted */))
    {
        // ... Do some stuff
        Method2(conn);
    }
}

void Method2(SqlConnection conn)
{
    // ... Do some more stuff
}
```
Method1() is the entry method which fires up an SqlConnection instance. This instance is then passed along to Method2() which uses the connection to do some more work. The method signature for Method2() includes the connection only because we want to reuse the connection, not because it has any relevance to the task which the method performs. This is just a very simple case, and there might as well have been multiple nested method calls with most of the method signatures being polluted with “unnessecary” parameters. One can easily imagine that more than one resource is required and thus 2 (or more) resource parameters will be passed along to all nested method calls; Ugh! Another horrible side-effect of this approach is that once you along the way find out that you actually need to access Method2() as the entry method, you find yourself writing a “dummy” method which simply instantiates the connection and passes it to the existing Method2(); or other equally undesirable work arounds.

This is why I decided to roll my own ambient resource to make my code more readable. The above example can be transformed into the following using my ambient resource utility:
 ```cs
// Create the factory somewhere appropriate (contructor, initialization code, etc.)
AmbientResource.ResourceFactory = () => new SqlConnection(/* connection string omitted */);

void Method1()
{
    using (var conn = AmbientResource.Current)
    {
        // ... Do some stuff
        Method2();
    }
}

void Method2()
{
    using (var conn = AmbientResource.Current)
    {
        // ... Do some more stuff
    }
}
```
This keeps the method signatures clean, and the ambient resource handles instantiating the resource and disposing it again when no longer used (i.e. exit from the “outer” using block).

##Ok, but how do I use a REAL resource?

Yeah ok, so the above example was mostly to provide a really short example which actually compiles and works. The problem is that AmbientResource class is very simple and simply encapsulates an instance of reference type IDisposable; thus not providing anything useful to the user. In effect the AmbientResource is mainly used as a proof-of-concept and should not be used for anything other than that.

Therefore the utility also includes a generic ambient resource, *GenericAmbientResource*, which is **not** intended to be used directly (therefore abstract), but rather to be extended.

So let’s continue with the above example. For this scenario I would create my own ambient resource class representing the type of resource I want to expose to my application (i.e. an SqlConnection):
```cs
class DbConnectionAmbientResource : GenericAmbientResource<DbConnectionAmbientResource, SqlConnection>
{
    public SqlCommand CreateCommand(string sql = null)
    {
        var cmd = Resource.CreateCommand();
        if (!string.IsNullOrEmpty(sql))
        {
            cmd.CommandText = sql;
        }
        return cmd;
    }
}
```
In such a class you have a choice of implementing “wrapper”/”delegation” methods (like I have done) to expose only the relevant methods of the “actual resource” (possibly with some additional around it), or to expose the “actual resource” itself via a property/method to provide the caller access to the real resource. I find the “wrapper methods” option to be most useful when refactoring existing code since you can then leave the existing code in place; it now just accesses the wrapper method on the ambient resource, rather than the actual method on the “actual resource” itself.

Using the class above, the example code can now be modified to:
```cs
// Create the factory somewhere appropriate (contructor, initialization code, etc.)
DbConnectionAmbientResource.ResourceFactory = () => new SqlConnection(/* connection string omitted */);

void Method1()
{
    using (var conn = DbConnectionAmbientResource.Current)
    {
        // ... Do some stuff
        var cmd = conn.CreateCommand();
        Method2();
    }
}

void Method2()
{
    using (var conn = DbConnectionAmbientResource.Current)
    {
        // ... Do some more stuff
        var cmd = conn.CreateCommand("SELECT * FROM MyTable");
    }
}
```
Now you are actually able to use the ambient resource for something useful (i.e. connect to a database).

##Where can I get it?

The utility is available as a NuGet package which can be installed from the Package Manager Console using the following command:

```
PM> Install-Package Nicolai.Resources.AmbientResource
```
You can also find the package on nuget.org.

##Important things to know

It is recommended to only use the ambient resource with a using block.
AmbientResource.Current can be used outside of a using block, but it requires that it is stored in a reference variable and that it is explicitly disposed (i.e. refVar.Dispose()) when no longer needed.
The generic ambient resource includes a timer mechanism which will Dispose() the resource if a timeout expires. The timer is reset every time a call to AmbientResource.Current is executed.
The default timeout is 30 seconds (i.e. 30.000 milliseconds) but can be adjusted to your liking by setting the AmbientResource.ResourceTimeMillis.

##Feedback

If you have any questions, comments or requests regarding this simple utility, please feel free to comment on this post. I will get back to you as soon as possible.