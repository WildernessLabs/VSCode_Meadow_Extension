import * as _ from "lodash";

import { parseString, selectPropertyPathItems } from "./xml-parsing";

export interface IMsBuildConfiguration {
  name: string;
  properties: { [name: string]: string };
}

export class MsBuildProjectAnalyzer {

  private xml: string;
  private parsedXml: any;

  constructor(xml: string) {
    this.xml = xml;
  }

  public async analyze(): Promise<void> {
    if (this.parsedXml) {
      return;
    }

    this.parsedXml = await parseString(this.xml);
  }

  public getReferences(): string[] {
    return selectPropertyPathItems<string>(this.parsedXml, ["Project", "ItemGroup", "Reference", "$", "Include"]);
  }

  public getProjectName(): string {
    return selectPropertyPathItems<string>(this.parsedXml, ["Project", "PropertyGroup", "AssemblyName"])[0];
  }


  public getPackageReferences(): string[] {
    return selectPropertyPathItems<string>(this.parsedXml, ["Project", "ItemGroup", "PackageReference", "$", "Include"]);
  }
  public getProjectReferences(): string[] {
    return selectPropertyPathItems<string>(this.parsedXml, ["Project", "ItemGroup", "ProjectReference", "$", "Include"]);
  }


  public getConfigurationNames(): string[] {
    return _
      .uniq(
      selectPropertyPathItems<string>(this.parsedXml, ["Project", "PropertyGroup", "$", "Condition"])
        .map(c => this.getPropertyValueFromCondition(c, "Configuration"))
        .filter(c => c))
      .sort();
  }

  public getSdk(): string {
    return this.parsedXml?.Project?.$?.Sdk;
  }

  public getPlatformNames(): string[] {
    return _
      .uniq(
      selectPropertyPathItems<string>(this.parsedXml, ["Project", "PropertyGroup", "$", "Condition"])
        .map(c => this.getPropertyValueFromCondition(c, "Platform"))
        .filter(c => c))
      .sort();
  }

  public getProperties(configuration: string, platform: string): { [name: string]: any } {
    let result: { [name: string]: any } = {
      "Configuration": configuration,
      "Platform": platform
    };

    let propertyGroups = selectPropertyPathItems(this.parsedXml, ["Project", "PropertyGroup"]);

    for (let i = 0; i < propertyGroups.length; i++) {
      this.visitPropertyGroup(propertyGroups[i], configuration, platform, result);
    }

    return _.mapValues(
      _.mapKeys(result, (v, k) => this.xmlToJsonPropertyName(k)),
      v => this.xmlToJsonPropertyValue(v));
  }

  private visitPropertyGroup(propertyGroupElement: any, configuration: string, platform: string, result: { [name: string]: any }): void {
    let condition = propertyGroupElement["$"] && propertyGroupElement["$"]["Condition"] as string;
    if (condition && !this.isConditionTrue(condition, result)) {
      return;
    }

    for (let propertyName in propertyGroupElement) {
      if (propertyName !== "$" && propertyName !== "_") {
        propertyGroupElement[propertyName].forEach(pe => {
          this.visitProperty(propertyName, pe, configuration, platform, result);
        });
      }
    }
  }

  private visitProperty(propertyName: string, propertyElement: any, configuration: string, platform: string, result: { [name: string]: any }): void {
    let condition = propertyElement["$"] && propertyElement["$"]["Condition"] as string;
    if (condition && !this.isConditionTrue(condition, result)) {
      return;
    }

    result[propertyName] = propertyElement["_"] || propertyElement;
  }

  private getPropertyValueFromCondition(condition: string, propertyName: string): string {
    let leftRight = this.splitCondition(condition);
    if (leftRight.length !== 2) {
      return null;
    }

    let propertyNames = leftRight[0].split("|");
    let propertyValues = leftRight[1].split("|");
    let propertyIndex = propertyNames.indexOf(`$(${propertyName})`);
    if (propertyIndex === -1 || propertyIndex >= propertyValues.length) {
      return null;
    }

    return propertyValues[propertyIndex];
  }

  private isConditionTrue(condition: string, properties: { [name: string]: string }): boolean {
    let leftRight = this.splitCondition(condition);
    if (leftRight.length !== 2) {
      return false;
    }

    let evaluatedLeft = this.eval(leftRight[0], properties);
    let evaluatedRight = this.eval(leftRight[1], properties);

    return evaluatedLeft === evaluatedRight;
  }

  private eval(expression: string, properties: { [name: string]: string }): string {
    let usedProperties = this.findPropertiesUsedInExpression(expression);
    return _.reduce(
      usedProperties,
      (result: string, property: string) => result.replace(new RegExp(`\\$\\(${property}\\)`, 'g'), properties[property] || ""),
      expression);
  }

  private findPropertiesUsedInExpression(expression: string): string[] {
    let re = new RegExp(/\(([^\)]+)\)/g);
    let result: string[] = [];
    let currentMatch: any = null;
    while (currentMatch = re.exec(expression)) {
      result.push(currentMatch[1]);
    }

    return result;
  }

  private splitCondition(condition: string): string[] {
    // Splits " \'$(Platform)|$(Configuration)\' == \'AnyCPU|Release\' " into:
    // [ '$(Platform)|$(Configuration)', 'AnyCPU|Release' ]
    return condition.split("==").map(c => c.trim().replace(new RegExp("\'", 'g'), ""));
  }

  private xmlToJsonPropertyName(propertyName: string): string {
    if (!(propertyName && propertyName.length > 0)) {
      return propertyName;
    }

    return propertyName[0].toLowerCase() + propertyName.slice(1);
  }

  private xmlToJsonPropertyValue(propertyValue: string): any {
    if (propertyValue === null || propertyValue === undefined) {
      return propertyValue;
    }

    if (typeof propertyValue !== "string") {
      return undefined;
    }

    let lower = propertyValue.toLowerCase();
    if (lower === "true") {
      return true;
    }
    if (lower === "false") {
      return false;
    }

    let number = Number(propertyValue);
    if (!isNaN(number.valueOf())) {
      return number.valueOf();
    }

    return propertyValue;
  }
}