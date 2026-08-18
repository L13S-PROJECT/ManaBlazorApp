
window.workflowGraph = {

    observe: function (workflowId) {

            const graph = document.getElementById(
                `workflow-graph-${workflowId}`);

            if (!graph || graph._workflowResizeObserver) {
                return;
            }

            const observer = new ResizeObserver(() => {

                if (!window.workflowGraph.lastDraw) {
                    return;
                }

                const { workflowId, connections } =
                    window.workflowGraph.lastDraw;

                window.workflowGraph.drawAfterLayout(
                    workflowId,
                    connections);
            });

            observer.observe(graph);

            graph._workflowResizeObserver = observer;
        },

    drawAfterLayout: function (workflowId, connections) {
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    window.workflowGraph.draw(workflowId, connections);
                });
            });
        },

    draw: function (workflowId, connections) {

        window.workflowGraph.lastDraw = {
                workflowId,
                connections
            };

        const graph = document.getElementById(
            `workflow-graph-${workflowId}`);

        const svg = document.getElementById(
            `workflow-connections-${workflowId}`);

        if (!graph || !svg) {
            return;
        }
        

        // Notīrām iepriekšējās līnijas
        svg.innerHTML = "";

        const graphRect = graph.getBoundingClientRect();

console.log(
    "GRAPH DRAW",
    "clientHeight:", graph.clientHeight,
    "scrollHeight:", graph.scrollHeight,
    "rectHeight:", graphRect.height
);

        const graphWidth = graph.scrollWidth;
        const graphHeight = graph.scrollHeight;

        svg.setAttribute(
            "viewBox",
            `0 0 ${graphWidth} ${graphHeight}`);

        svg.setAttribute("width", graphWidth);
        svg.setAttribute("height", graphHeight);

        // Arrow definīcija
        const defs = document.createElementNS(
            "http://www.w3.org/2000/svg",
            "defs");

        const marker = document.createElementNS(
            "http://www.w3.org/2000/svg",
            "marker");

        marker.setAttribute("id",
            `workflow-arrow-${workflowId}`);

        marker.setAttribute("markerWidth", "8");
        marker.setAttribute("markerHeight", "8");
        marker.setAttribute("refX", "8");
        marker.setAttribute("refY", "4");
        marker.setAttribute("orient", "auto");
        marker.setAttribute("markerUnits", "userSpaceOnUse");
        marker.setAttribute("viewBox", "0 0 8 8");

        const arrowPath = document.createElementNS(
            "http://www.w3.org/2000/svg",
            "path");

        arrowPath.setAttribute(
            "d",
            "M 0 0 L 8 4 L 0 8 z");

        arrowPath.setAttribute(
            "class",
            "workflow-connection-arrow");

        marker.appendChild(arrowPath);
        defs.appendChild(marker);
        svg.appendChild(defs);

        for (const connection of connections) {

            const fromElement = document.getElementById(
                `workflow-node-${connection.fromNodeId}`);

            const toElement = document.getElementById(
                `workflow-node-${connection.toNodeId}`);

            if (!fromElement || !toElement) {
                continue;
            }

            const fromNode =
                fromElement.querySelector(".flow-node");

            const toNode =
                toElement.querySelector(".flow-node");

            if (!fromNode || !toNode) {
                continue;
            }

            const fromRect =
                fromNode.getBoundingClientRect();

            const toRect =
                toNode.getBoundingClientRect();

            // START = apakšas centrs
            const startX =
                fromRect.left -
                graphRect.left +
                graph.scrollLeft +
                fromRect.width / 2;

            const startY =
                fromRect.bottom -
                graphRect.top +
                graph.scrollTop;

            const endX =
                toRect.left -
                graphRect.left +
                graph.scrollLeft +
                toRect.width / 2;

            const endY =
                toRect.top -
                graphRect.top +
                graph.scrollTop;

            drawConnection(
                svg,
                workflowId,
                connection,
                connections,
                startX,
                startY,
                endX,
                endY);

        }
    }
};


function drawConnection(
    svg,
    workflowId,
    connection,
    connections,
    startX,
    startY,
    endX,
    endY) {

    const path = document.createElementNS(
        "http://www.w3.org/2000/svg",
        "path");

    const verticalGap = endY - startY;

    const incomingConnections = connections.filter(
        x => x.toNodeId === connection.toNodeId);

    const isMergeTarget = incomingConnections.length > 1;

    let mergeY = null;

    if (isMergeTarget) {
        mergeY = endY - 24;
    }

    let d;

    // Ja abi mezgli praktiski vienā kolonnā,
    // zīmējam taisni.
    if (Math.abs(startX - endX) < 3) {

        d = `
            M ${startX} ${startY}
            L ${endX} ${endY}
        `;
    }
    else {

        // Horizontālā pāreja notiek pa vidu
        // starp abiem līmeņiem.
        const middleY = isMergeTarget
            ? mergeY
            : startY + Math.min(verticalGap / 2, 28);

        if (isMergeTarget) {

            d = `
                M ${startX} ${startY}
                L ${startX} ${middleY}
                L ${endX} ${middleY}
                L ${endX} ${endY}
            `;
        }
    else {

        d = `
            M ${startX} ${startY}
            L ${startX} ${middleY}
            L ${endX} ${middleY}
            L ${endX} ${endY}
        `;
    }
    }

    path.setAttribute("d", d);
    path.setAttribute("fill", "none");
    path.setAttribute("stroke", "#2f6fd6");
    path.setAttribute("stroke-width", "2");
    path.setAttribute("stroke-linecap", "round");
    path.setAttribute("stroke-linejoin", "round");

    path.setAttribute(
        "class",
        "workflow-connection");

    path.setAttribute(
        "marker-end",
        `url(#workflow-arrow-${workflowId})`);

    svg.appendChild(path);
}

window.addEventListener("resize", function () {
    if (!window.workflowGraph.lastDraw) {
        return;
    }

    const { workflowId, connections } =
        window.workflowGraph.lastDraw;

    window.workflowGraph.draw(
        workflowId,
        connections);
});