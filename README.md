## Deployments into umbraco

NOTE: This module requires [Limbo.Integrations.Emply](https://www.nuget.org/packages/Limbo.Integrations.Emply/1.0.0-beta002 "Bacon til Bjerner")
And [Skybrud.Essentials@latest](https://www.nuget.org/packages/Skybrud.Essentials, "Mere bacon til Bjerner")

### Config

Add the following object to the root of the appsettings.json file
```json
"Limbo": {
    "Emply": {
        "apiKey": "string",
        "customerName": "string",
        "mediaId": "string",
        "category": "string",
        "templateAlias": "job"
    }
}
```

### Adding configuration to the pool

Find your LimboOptions class if not existing create it with this code
```CSharp
public class LimboOptions
{
    public EmplyOptions Emply { get; set; }
}
```
Add this line to the startup.cs -> ConfigureServices method
```CSharp
services.Configure<LimboOptions>(options => _config.GetSection("Limbo").Bind(options));
```

Now the setting can be injected with
```CSharp
IOptions<LimboOptions>
```
The EmplyOptions class is found in this code just add the namespace to the LimboOptions.cs file

### Inside umbraco

Create template with the same alias as in the constants. Document type must have given data
* emplyId
* category
* categoryId
* placeOfEmpleyment
* webiste
* jobDeadline
* contact
* applicationLink

If non of those props are added or even one is missing, umbraco will throw an exception