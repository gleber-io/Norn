using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Norn.Executor.Kubernetes;
using Norn.Executor.Settings;
using Norn.Executor.UnitTests.TestFixtures;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Executor.UnitTests.Kubernetes;

/// <summary>
/// Tarefa 10 da Fase 9 — <see cref="IKubernetes"/> dublado, payload do patch verificado. As
/// convenientes <c>*Async</c> que o código de produção chama são extension methods sobre as
/// <c>*WithHttpMessagesAsync</c> — NSubstitute só intercepta membro de interface de verdade, então
/// o dublê é sempre configurado/verificado no nível baixo.
/// </summary>
public sealed class KubernetesActionApplierTests
{
    [Fact]
    public async Task ApplyScaleUpAsync_PatchesScaleSubresourceWithTargetReplicas()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var appsV1 = Substitute.For<IAppsV1Operations>();
        kubernetesClient.AppsV1.Returns(appsV1);
        appsV1.PatchNamespacedDeploymentScaleWithHttpMessagesAsync(Arg.Any<V1Patch>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1Scale> { Body = new V1Scale() });

        var options = new ExecutorOptions();
        var applier = new KubernetesActionApplier(kubernetesClient, options);
        var action = HealingActionBuilder.ScaleUp(replicaDelta: 1);

        var result = await applier.ApplyScaleUpAsync(action, targetReplicas: 2, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ActionApplyOutcome.Succeeded);
        await appsV1.Received(1).PatchNamespacedDeploymentScaleWithHttpMessagesAsync(
            Arg.Is<V1Patch>(p => p.Content != null && p.Content.ToString()!.Contains("\"replicas\":2")),
            "catalog-api",
            HealingActionBuilder.DefaultTarget.Namespace,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyScaleUpAsync_ForbiddenResponse_ReturnsRbacDefect()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var appsV1 = Substitute.For<IAppsV1Operations>();
        kubernetesClient.AppsV1.Returns(appsV1);
        appsV1.PatchNamespacedDeploymentScaleWithHttpMessagesAsync(Arg.Any<V1Patch>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<HttpOperationResponse<V1Scale>>(ForbiddenException()));

        var applier = new KubernetesActionApplier(kubernetesClient, new ExecutorOptions());
        var result = await applier.ApplyScaleUpAsync(HealingActionBuilder.ScaleUp(), targetReplicas: 2, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ActionApplyOutcome.RbacDefect);
    }

    [Fact]
    public async Task ApplyRestartPodAsync_DeletesPodByName()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var coreV1 = Substitute.For<ICoreV1Operations>();
        kubernetesClient.CoreV1.Returns(coreV1);
        coreV1.DeleteNamespacedPodWithHttpMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1Pod> { Body = new V1Pod() });

        var applier = new KubernetesActionApplier(kubernetesClient, new ExecutorOptions());
        var action = HealingActionBuilder.RestartPod("uid-1");

        var result = await applier.ApplyRestartPodAsync(action, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ActionApplyOutcome.Succeeded);
        await coreV1.Received(1).DeleteNamespacedPodWithHttpMessagesAsync(
            HealingActionBuilder.DefaultTarget.Pod, HealingActionBuilder.DefaultTarget.Namespace, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyRestartPodAsync_PodAlreadyGone_ReturnsRejectedNotFailed()
    {
        var kubernetesClient = Substitute.For<IKubernetes>();
        var coreV1 = Substitute.For<ICoreV1Operations>();
        kubernetesClient.CoreV1.Returns(coreV1);
        coreV1.DeleteNamespacedPodWithHttpMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<HttpOperationResponse<V1Pod>>(NotFoundException()));

        var applier = new KubernetesActionApplier(kubernetesClient, new ExecutorOptions());
        var result = await applier.ApplyRestartPodAsync(HealingActionBuilder.RestartPod("uid-1"), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(ActionApplyOutcome.Rejected);
    }

    private static HttpOperationException ForbiddenException() =>
        new("forbidden") { Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Forbidden), string.Empty) };

    private static HttpOperationException NotFoundException() =>
        new("not found") { Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.NotFound), string.Empty) };
}
