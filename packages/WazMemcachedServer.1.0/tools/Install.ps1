param($installPath, $toolsPath, $package, $project)
('amd64', 'x86') | %{ $project.ProjectItems.Item("memcached").ProjectItems.Item($_).ProjectItems } | %{
    $_.Properties.Item("BuildAction").Value = 0
    $_.Properties.Item("CopyToOutputDirectory").Value = 1
}