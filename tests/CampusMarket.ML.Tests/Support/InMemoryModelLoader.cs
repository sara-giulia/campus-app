using Microsoft.Extensions.ML;
using Microsoft.Extensions.Primitives;
using Microsoft.ML;

namespace CampusMarket.ML.Tests.Support;

// Gives a PredictionEnginePool a model that was built in memory instead of loaded from a file.
internal sealed class InMemoryModelLoader(ITransformer model) : ModelLoader
{
    private readonly ITransformer _model = model;
    private readonly IChangeToken _token = new CancellationChangeToken(CancellationToken.None);

    public override ITransformer GetModel() => _model;

    public override IChangeToken GetReloadToken() => _token;
}
