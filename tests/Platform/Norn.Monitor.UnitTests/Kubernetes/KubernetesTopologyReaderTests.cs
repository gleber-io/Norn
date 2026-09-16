using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Options;
using Norn.Monitor;
using Norn.Monitor.Kubernetes;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Monitor.UnitTests.Kubernetes;

/// <summary>
/// Achado do replay ao vivo da Fase 9: sem este método, nenhum <c>AnomalyContext</c> construído ao
/// vivo carrega um pod real — só o UUID de instância OTel que o Prometheus expõe. Cobre a resolução
/// de identidade que fecha essa lacuna (não o teste do bug em si, que vive documentado no CLAUDE.md).
/// </summary>
public sealed class KubernetesTopologyReaderTests
{
    private static KubernetesTopologyReader CreateReader(IKubernetes kubernetesClient) =>
        new(kubernetesClient, Options.Create(new MonitorOptions
        {
            PrometheusBaseUrl = "http://localhost:9090",
            ServiceToDeploymentName = new Dictionary<string, string> { ["Norn.Shop.Catalog.API"] = "catalog-api" },
        }));

    [Fact]
    public async Task GetCurrentPodNameAsync_OneRunningPod_ReturnsItsName()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var coreV1 = Substitute.For<ICoreV1Operations>();
        kubernetesClient.CoreV1.Returns(coreV1);
        coreV1.ListNamespacedPodWithHttpMessagesAsync(
                Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<int?>(), Arg.Any<bool?>(), Arg.Any<bool?>(), Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1PodList>
            {
                Body = new V1PodList
                {
                    Items =
                    [
                        new V1Pod { Metadata = new V1ObjectMeta { Name = "catalog-api-abc123" }, Status = new V1PodStatus { Phase = "Running" } },
                    ],
                },
            });

        var podName = await CreateReader(kubernetesClient)
            .GetCurrentPodNameAsync("Norn.Shop.Catalog.API", "norn-shop", TestContext.Current.CancellationToken);

        podName.ShouldBe("catalog-api-abc123");
    }

    [Fact]
    public async Task GetCurrentPodNameAsync_PodPendingAndPodRunning_PrefersRunning()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var coreV1 = Substitute.For<ICoreV1Operations>();
        kubernetesClient.CoreV1.Returns(coreV1);
        coreV1.ListNamespacedPodWithHttpMessagesAsync(
                Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<int?>(), Arg.Any<bool?>(), Arg.Any<bool?>(), Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1PodList>
            {
                Body = new V1PodList
                {
                    Items =
                    [
                        new V1Pod { Metadata = new V1ObjectMeta { Name = "catalog-api-pending" }, Status = new V1PodStatus { Phase = "Pending" } },
                        new V1Pod { Metadata = new V1ObjectMeta { Name = "catalog-api-running" }, Status = new V1PodStatus { Phase = "Running" } },
                    ],
                },
            });

        var podName = await CreateReader(kubernetesClient)
            .GetCurrentPodNameAsync("Norn.Shop.Catalog.API", "norn-shop", TestContext.Current.CancellationToken);

        podName.ShouldBe("catalog-api-running");
    }

    [Fact]
    public async Task GetCurrentPodNameAsync_NoPods_ReturnsNull()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var coreV1 = Substitute.For<ICoreV1Operations>();
        kubernetesClient.CoreV1.Returns(coreV1);
        coreV1.ListNamespacedPodWithHttpMessagesAsync(
                Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<int?>(), Arg.Any<bool?>(), Arg.Any<bool?>(), Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1PodList> { Body = new V1PodList { Items = [] } });

        var podName = await CreateReader(kubernetesClient)
            .GetCurrentPodNameAsync("Norn.Shop.Catalog.API", "norn-shop", TestContext.Current.CancellationToken);

        podName.ShouldBeNull();
    }
}
